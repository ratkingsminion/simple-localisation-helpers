# slh
Simple localisation helpers for Unity/C# - uses JSON files

Usage:

```JSON
{
  "config": {
    "cam": "Camera",
    "audio": [ "Music", "Sound" ],
    "credits": {
      "coder": "Programmer",
      "music": "Composer",
      "gfx": "Graphics artist"
    }
  }
}
```

```C#
  [SerializeField] TMPro.TextMeshProUGUI uiLabel = null;
  [SerializeField] string locaKey = "credits/coder";
  [SerializeField] int locaKeyIndex = 0;
  
  void Start() {
    uiLabel.text = SLH.Localisation.Do("config/" + locaKey, locaKeyIndex);
  }
```
