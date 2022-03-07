using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RatKing.SLH {

	[System.Serializable]
	public class LocalisationLanguage {
		public SystemLanguage language;
		public string name;
		public string code;
		public List<TextAsset> textAssets;
		public List<string> folderNames;
		// only during runtime:
		[System.NonSerialized] public List<string> filesToLoad;
		[System.NonSerialized] public SimpleJSON.JSONNode json;
	}
	
	[DefaultExecutionOrder(-5000)]
	public class Localisation : MonoBehaviour {

		/// <summary>
		/// Gets called whenever the current language is changed
		/// </summary>
		public static System.Action OnLanguageChanged { get; set; } = null;
		public static Localisation Inst { get; private set; }

		[SerializeField, Tooltip("Optional: enter a definitions file (JSON) that will define what languages exist")] string definitionsFileName = "";
		[SerializeField, Tooltip("If you don't have a definitions file you need to add all languages here.")] LocalisationLanguage[] languages = null;
		//
		public static List<LocalisationLanguage> Languages { get; private set; }
		public static int CurLanguageIndex { get; private set; } = 0;
		public static SystemLanguage CurLanguage => Languages[CurLanguageIndex].language;
		//
		static Dictionary<string, string[]> textsByKey = new Dictionary<string, string[]>();
		static System.Action localisationCallbacks = null;
		//
		static readonly char[] keyTrimmer = new[] { '\\', '/', '\n', '\r', '\t', '"', ' ' };

		//

		void Awake() {
			if (Inst != null) { Debug.LogError("More than one Localisation instance in the scene - deleting!"); Destroy(this); return; }
			Inst = this;
			
#if UNITY_EDITOR
			var dataPath = Application.dataPath + "/";
#else
			var dataPath = Application.dataPath + "/../";
#endif

			Languages = languages != null ? new List<LocalisationLanguage>(languages) : new List<LocalisationLanguage>();

			// collect the needed files via the definition file

			if (!string.IsNullOrWhiteSpace(definitionsFileName) && System.IO.File.Exists(dataPath + "/" + definitionsFileName)) {
				var definitionString = System.IO.File.ReadAllText(dataPath + "/" + definitionsFileName);
				var jsonDefinitions = SimpleJSON.JSONNode.Parse(definitionString);
				if (!jsonDefinitions.IsObject) { Debug.LogError("Malformed localisations definition file"); return; }
				foreach (var key in jsonDefinitions.Keys) {
					var jsonLoca = jsonDefinitions[key];
					if (!jsonLoca.IsObject) { Debug.LogWarning("Localisation object " + key + " is malformed (" + jsonLoca.ToString() + ")"); continue; }

					var code = jsonLoca["code"];
					var language = Languages.Find(l => l.code == code);
					if (language == null) {
						Languages.Add(language = new LocalisationLanguage() {
							language = (SystemLanguage)System.Enum.Parse(typeof(SystemLanguage), key, true),
							name = jsonLoca["name"].Value,
							code = code,
							textAssets = null,
							folderNames = null,
							filesToLoad = new List<string>(),
							json = new SimpleJSON.JSONObject()
						});
					}
					
					var files = jsonLoca["files"].IsArray ? jsonLoca["files"].AsStringArray : new[] { jsonLoca["files"].Value };
					foreach (var file in files) {
						var filePath = dataPath + "/" + file;
						if (!System.IO.File.Exists(filePath)) { Debug.LogWarning("Localisation file for " + key + " does not exist"); continue; }
						language.filesToLoad.Add(filePath);
					}
					
					var folders = jsonLoca["folders"].IsArray ? jsonLoca["folders"].AsStringArray : new[] { jsonLoca["folders"].Value };
					foreach (var f in folders) { language.folderNames.Add(dataPath + "/" + f); }
				}
			}
			
			// collect further data via folders
			foreach (var lang in Languages) {
				if (lang.folderNames != null && lang.folderNames.Count > 0) {
					foreach (var folder in lang.folderNames) {
						var path = dataPath + "/" + folder;
						if (!System.IO.Directory.Exists(path)) { Debug.LogError("Folder path " + path + " does not exist!"); continue; }
						foreach (var filePath in System.IO.Directory.GetFiles(path, "*.json", System.IO.SearchOption.AllDirectories)) {
							if (lang.filesToLoad == null) { lang.filesToLoad = new List<string>(); }
							lang.filesToLoad.Add(filePath);
						}
					}
				}
			}

			InitLocalisation(0);
			localisationCallbacks?.Invoke();
		}

		void OnDestroy() {
			CurLanguageIndex = 0;
			textsByKey.Clear();
		}

		void InitLocalisation(int index) {
			if (Languages == null || Languages.Count == 0) { Debug.LogError("No localisations present!"); return; }
			if (index < 0 || index >= Languages.Count) { Debug.LogError("Wrong localisation index!"); return; }
			var language = Languages[index];

			if (language.json == null) {
				language.json = new SimpleJSON.JSONObject();

				void MergeNodes(SimpleJSON.JSONNode to, SimpleJSON.JSONNode source) {
					foreach (var kvp in source) {
						if (to.HasKey(kvp.Key)) { MergeNodes(to[kvp.Key], kvp.Value); }
						else { to.Add(kvp.Key, kvp.Value); }
					}
				}

				if (language.textAssets != null) {
					foreach (var file in language.textAssets) {
						var fileJSON = SimpleJSON.JSONNode.Parse(file.text);
						if (!fileJSON.IsObject) { Debug.LogWarning("Localisation file + " + file.name + " for " + language.language + " is malformed"); continue; }
						MergeNodes(language.json, fileJSON);
					}
				}

				if (language.filesToLoad?.Count > 0) {
					foreach (var file in language.filesToLoad) {
						var fileContent = System.IO.File.ReadAllText(file);
						var jsonFile = SimpleJSON.JSONNode.Parse(fileContent);
						if (!jsonFile.IsObject) { Debug.LogWarning("Localisation file " + file + " for " + language.language + " is malformed"); continue; }
						MergeNodes(language.json, jsonFile);
					}
				}
			}

			GetLocalisationKeys("", language.json);
		}

		void GetLocalisationKeys(string curKey, SimpleJSON.JSONNode field) {
			if (field.IsObject) {
				foreach (var key in field.Keys) {
					GetLocalisationKeys((curKey + "/" + key).Trim(keyTrimmer), field[key]);
				}
			}
			else if (field.IsArray) {
				var list = new List<string>(field.AsArray.Count);
				foreach (var sub in field.AsArray) {
					if (sub.Value.IsString) { list.Add(sub.Value); }
				}
				if (list.Count > 0) {
					textsByKey[curKey] = list.ToArray();
				}
			}
			else if (field.IsString) {
				textsByKey[curKey] = new[] { field.Value };
			}
		}

		//

		public static void ChangeLanguage(string code) {
			if (Inst == null) { Debug.LogWarning("Could not change language because instance is null! Call this later or change this script's execution order to be as early as possible."); return; }

			var index = Languages.FindLastIndex(l => l.code == code);
			if (index < 0) { Debug.LogWarning("Language '" + code + "' not found!"); return; }
			else { ChangeLanguage(index); }
		}

		public static void ChangeLanguage(int index) {
			if (Inst == null) { Debug.LogWarning("Could not change language because instance is null! Call this later or change this script's execution order to be as early as possible."); return; }
			if (CurLanguageIndex == index) { return; }
			Inst.InitLocalisation(index);
			CurLanguageIndex = index;
			localisationCallbacks?.Invoke();
			OnLanguageChanged?.Invoke();
		}

		//

		/// <summary>
		/// Similar to just registering the callback to OnLanguageChanged,
		/// but will also call it directly if the localisation is prepared already
		/// </summary>
		/// <param name="localisationCallback">the method to be called</param>
		public static void RegisterCallback(System.Action localisationCallback) {
			localisationCallbacks += localisationCallback;
			if (Inst != null) { localisationCallback?.Invoke(); }
		}

		public static void UnregisterCallback(System.Action localisationCallback) {
			localisationCallbacks -= localisationCallback;
		}

		public static string Do(string key, int idx, bool convertSpecial = true) {
#if UNITY_EDITOR
			if (textsByKey.Count == 0) {  Debug.LogError("Trying to get key " + key + " before localisation inited!"); return ""; }
			if (idx < 0) { return "'" + key + "' WRONG IDX!"; }
#endif
			if (!textsByKey.TryGetValue(key, out var texts)) { return "'" + key + "' NOT FOUND!"; }
			if (texts.Length >= idx) { return "'" + key + "' NO IDX " + idx + "!"; }
			if (convertSpecial) { return texts[idx].Replace("\\n", "\n").Replace("\\t", "\t"); }
			return texts[idx];
		}

		public static string Do(string key, bool convertSpecial = true) {
#if UNITY_EDITOR
			if (textsByKey.Count == 0) {  Debug.LogError("Trying to get key " + key + " before localisation inited!"); return ""; }
#endif
			if (!textsByKey.TryGetValue(key, out var texts)) { return "'" + key + "' NOT FOUND!"; }
			if (convertSpecial) { return texts[0].Replace("\\n", "\n").Replace("\\t", "\t"); }
			return texts[0];
		}

		public static int TryGetAll(string key, out string[] result) {
#if UNITY_EDITOR
			if (textsByKey.Count == 0) {  Debug.LogError("Trying to get key " + key + " before localisation inited!");  result = null; return 0; }
#endif
			if (textsByKey.TryGetValue(key, out result)) { return result.Length; }
			return 0;
		}

		public static string ConvertSpecial(string text) {
			return text.Replace("\\n", "\n").Replace("\\t", "\t");
		}

		public static void Set(string key, string value, int idx) {
			if (textsByKey.TryGetValue(key, out var texts)) { texts[idx] = value; }
			else { textsByKey[key] = new string[idx + 1]; textsByKey[key][idx] = value; }
		}

		public static void Set(string key, string value) {
			if (textsByKey.TryGetValue(key, out var texts)) { texts[0] = value; }
			else { textsByKey[key] = new[] { value }; }
		}
	}

}