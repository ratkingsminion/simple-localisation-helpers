# slh
Simple localisation helpers for Unity/C# - uses JSON files

Usage:

```JSON
{
  "config": {
    "cam": "Camera",
    "audio": [ "Music", "Sound" ],
    "credits": {
      "code": "Programmer",
      "music": "Composer",
      "gfx": "Graphics artist"
    }
  }
}
```

```C#
  [SerializeField] TMPro.TextMeshProUGUI uiLabel = null;
  [SerializeField] string locaKey = "credits/code";
  [SerializeField] int locaKeyIndex = 0;
  
  void Start() {
    uiLabel.text = SLH.Localisation.Do("config/" + locaKey, locaKeyIndex);
	// In order to set the label whenever the language is changed use SLH.Localisation.RegisterCallback()
	// and/or the SLH.Localisation.OnLanguageChanged callback
  }
```

Example for an optional definitions file:

```JSON
{
  "English": {
    "name": "English",
    "code": "en",
    "files": "English_complete.json"
  },
  "German": {
    "name": "Deutsch",
    "code": "de",
    "files": [ "German_interface.json", "German_dialogs.json" ]
  }
}
```