using System;

[Serializable]
public class ParticleData
{
    public string id;
    public string name;
    public string icon;    // path in Resources (opzionale)
    public string prefab;   // path in Resources (opzionale)
}

[Serializable]
public class ParticleList
{
    public ParticleData[] Particles;
}