﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System;

public class All : MonoBehaviour {

    public GameObject prefabDragItem;

    TcpListener listener;
    Recognition recognition;
    List<string> events;
    object eventsMutex;
    
    Text tSample;
    Text tInputed;
    Text[] tDragItems;
    string infoText;

    List<string> inputedWords;
    List<Vector2> inputedPoints;
    string[] sampleSentences;
    int sampleIndex;
    string[] candidates;

    int deviceWidth, deviceHeight;
    const int DRAG_ROW = 5;
    const int DRAG_COLUMN = 5;
    const int DRAG_ITEM_N = DRAG_ROW * DRAG_COLUMN;
    const float DRAG_SMOOTH = 1.0f;
    int dragStartX, dragStartY;
    int dragSpanX, dragSpanY;
    int selectX, selectY, selectIndex;

    void Start()
    {
        SetupServer();
        recognition = new Recognition();
        events = new List<string>();
        eventsMutex = new object();
        
        tSample = GameObject.Find("Sample").GetComponent<Text>();
        tInputed = GameObject.Find("Inputed").GetComponent<Text>();
        tDragItems = new Text[DRAG_ITEM_N];
        GameObject canvas = GameObject.Find("Canvas");
        for (int i = 0; i < DRAG_ROW; i++)
            for (int j = 0; j < DRAG_COLUMN; j++)
            {
                GameObject gDragItem = Instantiate(prefabDragItem);
                gDragItem.transform.position = new Vector3(-150 + j * 105, -15 + i * -35, 500);
                gDragItem.transform.SetParent(canvas.transform);
                tDragItems[i * DRAG_COLUMN + j] = gDragItem.GetComponentInChildren<Text>();
            }

        inputedWords = new List<string>();
        inputedPoints = new List<Vector2>();
        sampleSentences = XFileReader.Read("phrases-normal.txt");
        for (int i = 0; i < sampleSentences.Length; i++) sampleSentences[i] = sampleSentences[i].ToLower();
        sampleIndex = -1;
        selectIndex = 0;
        
        UpdateSample();
        UpdateInputed();
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            infoText = Input.mousePosition.x + " " + Input.mousePosition.y;
        }
        //infoText = new System.Random().Next().ToString();
        GameObject.Find("Info").GetComponent<Text>().text = infoText;
        lock (eventsMutex)
        {
            for (int i = 0; i < events.Count; i++)
            {
                string line = events[i];
                if (i < events.Count - 1 && line.Substring(0, 5) == "drag " && events[i + 1].Substring(0, 5) == "drag ") continue;
                string[] arr = line.Split(' ');
                switch (arr[0])
                {
                    case "devicesize":
                        deviceWidth = int.Parse(arr[1]);
                        deviceHeight = int.Parse(arr[2]);
                        break;
                    case "click":
                        Click(int.Parse(arr[1]), int.Parse(arr[2]));
                        break;
                    case "dragbegin":
                        DragBegin(int.Parse(arr[1]), int.Parse(arr[2]));
                        break;
                    case "drag":
                        Drag(int.Parse(arr[1]), int.Parse(arr[2]));
                        break;
                    case "dragend":
                        DragEnd(int.Parse(arr[1]), int.Parse(arr[2]));
                        break;
                    case "leftslip":
                        LeftSlip();
                        break;
                    case "rightslip":
                        RightSlip();
                        break;
                    case "downslip":
                        DownSlip();
                        break;
                    case "actiondown":
                        break;
                }
            }
            events.Clear();
        }
    }

    void Click(int x, int y)
    {
        inputedPoints.Add(new Vector2(x, y));
        UpdateInputed();
    }

    void LeftSlip()
    {
        if (inputedPoints.Count == 0 && inputedWords.Count != 0) inputedWords.RemoveAt(inputedWords.Count - 1);
        if (inputedPoints.Count > 0) inputedPoints.RemoveAt(inputedPoints.Count - 1);
        UpdateInputed();
    }

    void RightSlip()
    {
        string[] sampleWords = sampleSentences[sampleIndex].Split(' ');
        if (inputedPoints.Count == 0 && inputedWords.Count == sampleWords.Length)
        {
            inputedWords.Clear();
            UpdateSample();
        }
        if (inputedPoints.Count > 0)
        {
            inputedWords.Add(candidates[0]);
            inputedPoints.Clear();
        }
        UpdateInputed();
    }

    void DownSlip()
    {
        inputedPoints.Clear();
        UpdateInputed();
    }

    void DragBegin(int x, int y)
    {
        if (inputedPoints.Count == 0) return;
        dragStartX = x;
        dragStartY = y;
        dragSpanX = Math.Min(Math.Max((deviceWidth - x - 40) / DRAG_COLUMN, 10), 80);
        dragSpanY = Math.Min(Math.Max((deviceHeight - y - 80) / DRAG_ROW, 10), 80);
    }

    void Drag(int x, int y)
    {
        if (inputedPoints.Count == 0) return;
        float addition = DRAG_SMOOTH - 0.5f;
        float selectX2 = 1.0f * (x - dragStartX) / dragSpanX;
        float selectY2 = 1.0f * (y - dragStartY) / dragSpanY;
        selectX2 = Math.Min(Math.Max(selectX2, -addition), DRAG_COLUMN + addition);
        selectY2 = Math.Min(Math.Max(selectY2, -addition), DRAG_ROW + addition);
        if (Math.Abs(selectX2 - (selectX + 0.5)) > DRAG_SMOOTH)
        {
            selectX = (x - dragStartX) / dragSpanX;
            selectX = Math.Min(Math.Max(selectX, 0), DRAG_COLUMN - 1);
        }
        if (Math.Abs(selectY2 - (selectY + 0.5)) > DRAG_SMOOTH)
        {
            selectY = (y - dragStartY) / dragSpanY;
            selectY = Math.Min(Math.Max(selectY, 0), DRAG_ROW - 1);
        }
        selectIndex = selectY * DRAG_COLUMN + selectX;
        selectIndex = Math.Min(selectIndex, candidates.Length - 1);
        UpdateInputed();
    }

    void DragEnd(int x, int y)
    {
        if (inputedPoints.Count == 0) return;
        Drag(x, y);
        inputedWords.Add(candidates[selectIndex]);
        inputedPoints.Clear();
        selectIndex = 0;
        UpdateInputed();
    }

    void UpdateInputed()
    {
        Color cIdle = new Color(0.5f, 0.9f, 1.0f);
        Color cSelected = new Color(1.0f, 0.8f, 0.0f);
        string inputedTot = "";
        foreach (string word in inputedWords) inputedTot += word + " ";
        if (inputedPoints.Count > 0)
        {
            candidates = recognition.Recognize(inputedPoints);
            inputedTot += "<color=#ff0000><b>" + candidates[0] + "</b></color>";
            for (int i = 0; i < DRAG_ITEM_N; i++)
            {
                tDragItems[i].text = candidates[i];
                tDragItems[i].transform.parent.GetComponent<Image>().color = (i == selectIndex) ? cSelected : cIdle;
            }
        }
        else
        {
            candidates = null;
            for (int i = 0; i < DRAG_ITEM_N; i++)
            {
                tDragItems[i].text = "";
                tDragItems[i].transform.parent.GetComponent<Image>().color = cIdle;
            }
        }
        inputedTot += "<color=#ff5555>|</color>";
        tInputed.text = inputedTot;
    }
    
    void UpdateSample()
    {
        sampleIndex = (sampleIndex + 1) % sampleSentences.Length;
        tSample.text = sampleSentences[sampleIndex];
    }

    void SetupServer()
    {
        int serverPort = 10309;
        string hostName = Dns.GetHostName();
        IPAddress[] addressList = Dns.GetHostAddresses(hostName);
        string serverIP = null;
        foreach (IPAddress ip in addressList)
        {
            if (ip.ToString().IndexOf("192.168.") != -1)
            {
                serverIP = ip.ToString();
                break;
            }
            if (ip.ToString().Substring(0, 3) != "127" && ip.ToString().Split('.').Length == 4) serverIP = ip.ToString();
        }
        Debug.Log("setup:" + serverIP + "," + serverPort);
        infoText = serverIP;
        listener = new TcpListener(IPAddress.Parse(serverIP), serverPort);
        Thread listenThread = new Thread(ListenThread);
        listenThread.Start();
    }

    void ListenThread()
    {
        listener.Start();
        while (true)
        {
            TcpClient client = listener.AcceptTcpClient();
            Thread receiveThread = new Thread(ReceiveThread);
            receiveThread.Start(client);
        }
    }

    void ReceiveThread(object clientObject)
    {
        infoText = "client in";
        Debug.Log("client in");
        TcpClient client = (TcpClient)clientObject;
        StreamReader reader = new StreamReader(client.GetStream());
        while (true)
        {
            string line;
            try
            {
                line = reader.ReadLine();
                if (line == null) break;
            }
            catch
            {
                break;
            }
            lock (eventsMutex)
            {
                events.Add(line);
            }
        }
        reader.Close();
        infoText = "client out";
        Debug.Log("client out");
    }
}

class XFileReader
{
    public static string[] Read(string filepath)
    {
        if (Application.platform == RuntimePlatform.WindowsEditor)
        {
            StreamReader reader = new StreamReader(new FileStream(Application.streamingAssetsPath + "/" + filepath, FileMode.Open));
            List<string> lines = new List<string>();
            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) break;
                lines.Add(line);
            }
            reader.Close();
            return lines.ToArray();
        }
        else if (Application.platform == RuntimePlatform.Android)
        {
            string url = Application.streamingAssetsPath + "/" + filepath;
            WWW www = new WWW(url);
            while (!www.isDone) { }
            return www.text.Split('\n');
        }
        return new string[0];
    }
}