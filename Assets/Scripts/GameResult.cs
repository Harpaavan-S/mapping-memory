using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameResult
{
    public string gameName;
    public float accuracy;
    public float avgReactionTime;
    
    // Additional metrics (optional)
    public float score;
    public string completionTime;
    public Dictionary<string, float> customMetrics;
    
    // Constructor for easy creation
    public GameResult(string name, float acc, float rt, float scr = 0)
    {
        gameName = name;
        accuracy = acc;
        avgReactionTime = rt;
        score = scr;
        completionTime = System.DateTime.Now.ToString("HH:mm:ss");
        customMetrics = new Dictionary<string, float>();
    }
}