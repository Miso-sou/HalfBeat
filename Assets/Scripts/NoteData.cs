[System.Serializable]
public class NoteData
{
    public float beat;    // When the cube should be hit (e.g., 4.5)
    public int lane;      // -1 (Left), 0 (Center), 1 (Right)
    public int layer;     // 0 (Bottom), 1 (Middle), 2 (Top)
}

[System.Serializable]
public class LevelData
{
    public string songName;
    public float bpm;
    public System.Collections.Generic.List<NoteData> notes;
}