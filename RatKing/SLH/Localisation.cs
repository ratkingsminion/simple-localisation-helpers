using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RatKing.SLH {

	[System.Serializable]
	public struct LocalisationLanguage {
		public SystemLanguage language;
		public string name;
		public string code;
		public TextAsset[] files;
		// only during runtime:
		[System.NonSerialized] public SimpleJSON.JSONNode json;
	}
	
	[DefaultExecutionOrder(-5000)]
	public class Localisation : MonoBehaviour {

		/// <summary>
		/// Gets called whenever the current language is changed
		/// </summary>
		public static System.Action OnLanguageChanged { get; set; } = null;
		public static Localisation Inst { get; private set; }

		[SerializeField] int keyPartCount = 16; // 16 should be more than enough anyway
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
			Inst = this;
			InitLocalisation(0);
			localisationCallbacks?.Invoke();
		}

		void OnDestroy() {
			CurLanguageIndex = 0;
			textsByKey.Clear();
		}

		void InitLocalisation(int index) {
			var hasDefinitionFile = !string.IsNullOrWhiteSpace(definitionsFileName);
			if (!hasDefinitionFile && (languages == null || languages.Length == 0)) {
				Debug.LogError("No localisations present");
				return;
			}

			if (Languages == null) {
				if (languages != null) { Languages = new List<LocalisationLanguage>(languages); }
				else { Languages = new List<LocalisationLanguage>(); }
			}
			
#if UNITY_EDITOR
			var dataPath = Application.dataPath + "/";
#else
			var dataPath = Application.dataPath + "/../";
#endif

			void MergeNodes(SimpleJSON.JSONNode to, SimpleJSON.JSONNode source) {
				foreach (var kvp in source) {
					if (to.HasKey(kvp.Key)) { MergeNodes(to[kvp.Key], kvp.Value); }
					else { to.Add(kvp.Key, kvp.Value); }
				}
			}

			if (hasDefinitionFile && Languages.Count == 0) {
				var definitionString = System.IO.File.ReadAllText(dataPath + "/" + definitionsFileName);
				var json = SimpleJSON.JSONNode.Parse(definitionString);
				if (!json.IsObject) { Debug.LogError("Malformed localisations definition file"); return; }
				foreach (var key in json.Keys) {
					var jsonLang = json[key];
					if (!jsonLang.IsObject) { Debug.LogWarning("Localisation object " + key + " is malformed (" + jsonLang.ToString() + ")"); continue; }

					SimpleJSON.JSONNode languageJSON = new SimpleJSON.JSONObject();
					
					string[] files = jsonLang["files"].IsArray ? jsonLang["files"].AsStringArray : new[] { jsonLang["files"].Value };
					foreach (var fileName in files) {
						var filePath = dataPath + "/" + fileName;
						if (!System.IO.File.Exists(filePath)) { Debug.LogWarning("Localisation file for " + key + " does not exist"); continue; }
						var fileContent = System.IO.File.ReadAllText(filePath);
						var fileJSON = SimpleJSON.JSONNode.Parse(fileContent);
						if (!fileJSON.IsObject) { Debug.LogWarning("Localisation file for " + key + " is malformed"); continue; }
						MergeNodes(languageJSON, fileJSON);
					}
					
					Languages.Add(new LocalisationLanguage() {
						language = (SystemLanguage)System.Enum.Parse(typeof(SystemLanguage), key, true),
						name = jsonLang["name"].Value,
						code = jsonLang["code"].Value,
						files = null,
						json = languageJSON
					});
				}
			}

			var language = Languages[index];
			if (language.files == null && (language.json == null || !language.json.IsObject)) {
				Debug.LogWarning("No valid definitions for localisation of " + language.language);
			}
			else {
				if (language.json == null) {
					language.json = new SimpleJSON.JSONObject();
					foreach (var file in language.files) {
						var fileJSON = SimpleJSON.JSONNode.Parse(file.text);
						if (!fileJSON.IsObject) { Debug.LogWarning("Localisation file for " + language.language + " is malformed"); continue; }
						MergeNodes(language.json, fileJSON);
					}
				}

				GetLocalisationKeys(new string[keyPartCount], 0, language.json);
			}
		}

		void GetLocalisationKeys(string[] curKeyParts, int keyPartsCount, SimpleJSON.JSONNode field) {
			if (field.IsObject) {
				foreach (var key in field.Keys) {
					curKeyParts[keyPartsCount] = key;
					GetLocalisationKeys(curKeyParts, keyPartsCount + 1, field[key]);
				}
			}
			else if (field.IsArray) {
				var curKey = string.Join("/", curKeyParts, 0, keyPartsCount).Trim(keyTrimmer);
				var list = new List<string>(field.AsArray.Count);
				foreach (var sub in field.AsArray) {
					if (sub.Value.IsString) { list.Add(sub.Value); }
				}
				if (list.Count > 0) {
					textsByKey[curKey] = list.ToArray();
				}
			}
			else if (field.IsString) {
				var curKey = string.Join("/", curKeyParts, 0, keyPartsCount).Trim(keyTrimmer);
				textsByKey[curKey] = new[] { field.Value };
			}
		}

		//

		public static void ChangeLanguage(string code) {
			var index = Languages.FindLastIndex(l => l.code == code);
			if (index < 0) { Debug.LogWarning("Language " + code + " not found!"); return; }
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