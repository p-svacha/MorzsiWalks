using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class JsonUtilities
{
    private const string SAVE_GAME_PATH = "Assets/Data/";

    public static void SaveMap(MapData data)
    {
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        string path = SAVE_GAME_PATH + data.Name + ".json";
        File.WriteAllText(path, json);
        Debug.Log("Successfully saved " + data.Name + " data:\n\n" + json);
    }

    public static MapData LoadGame(string name)
    {
        string jsonFilePath = SAVE_GAME_PATH + name + ".json";
        MapData data = null;

        using (StreamReader r = new StreamReader(jsonFilePath))
        {
            string json = r.ReadToEnd();
            data = JsonConvert.DeserializeObject<MapData>(json);
        }

        if (data == null) throw new System.Exception("Didn't find the save file " + jsonFilePath);
        return data;
    }
}
