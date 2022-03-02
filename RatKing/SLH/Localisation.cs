using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace RatKing.Base {

	[System.Serializable]
	public struct LocalisationLanguage {
		public SystemLanguage language;
		public string name;
		public string code;
		public TextAsset file;
		[System.NonSerialized] public string fileContent;
	}
	
	[DefaultExecutionOrder(-5000)]
	public class Localisation : MonoBehaviour {

		public static Base.Signal LANGUAGE_CHANGED = new Base.Signal();

		public static Localisation Inst { get; private set; }
		//
		[SerializeField] int keyPartCount = 16; // 16 should be more than enough anyway
		[SerializeField, Tooltip("Optional: enter a definition file (JSON) that will define what languages exist")] string definitionFileName = "";
		[SerializeField, Tooltip("If you don't have a definition file you need to add all languages here.")] LocalisationLanguage[] languages = null;
		//
		public static List<LocalisationLanguage> Languages { get; private set; }
		public static int CurLanguageIndex { get; private set; } = 0;
		public static SystemLanguage CurLanguage => Languages[CurLanguageIndex].language;
		//
		static Dictionary<string, string[]> textsByKey = new Dictionary<string, string[]>();
		static List<ILocaliseComponent> localisations = new List<ILocaliseComponent>(32);
		//
		static readonly char[] keyTrimmer = new[] { '\\', '/', '\n', '\r', '\t', '"', ' ' };
		static bool addedDefinitions = false;

		//

		void Awake() {
			Inst = this;
			InitTranslations(0);
			LocaliseAll();
		}

		void OnDestroy() {
			CurLanguageIndex = 0;
			textsByKey.Clear();
		}

		void InitTranslations(int index) {
			var hasDefinitionFile = !string.IsNullOrWhiteSpace(definitionFileName);
			if (!hasDefinitionFile && (languages == null || languages.Length == 0)) {
				Debug.LogError("No localisations present");
				return;
			}
			//
			if (Languages == null) {
				if (languages != null) { Languages = new List<LocalisationLanguage>(languages); }
				else { Languages = new List<LocalisationLanguage>(); }
			}
			if (hasDefinitionFile && !addedDefinitions) {
#if UNITY_EDITOR
				var dataPath = Application.dataPath + "/";
#else
				var dataPath = Application.dataPath + "/../";
#endif
				var definitionString = System.IO.File.ReadAllText(dataPath + "/" + definitionFileName);
				var json = SimpleJSON.JSONNode.Parse(definitionString);
				if (!json.IsObject) { Debug.LogError("Malformed localisations definition file"); return; }
				foreach (var key in json.Keys) {
					var jsonLang = json[key];
					if (!jsonLang.IsObject) { Debug.LogWarning("Localisation object " + key + " is malformed (" + jsonLang.ToString() + ")"); continue; }
					var fileName = jsonLang["file"].Value;
					var filePath = dataPath + "/" + fileName;
					if (!System.IO.File.Exists(filePath)) { Debug.LogWarning("Localisation file for " + key + " does not exist"); continue; }
					var fileContent = System.IO.File.ReadAllText(filePath);
					if (string.IsNullOrWhiteSpace(fileContent)) { Debug.LogWarning("Localisation file for " + key + " is malformed"); continue; }
					Languages.Add(new LocalisationLanguage() {
						language = (SystemLanguage)System.Enum.Parse(typeof(SystemLanguage), key, true),
						name = jsonLang["name"].Value,
						code = jsonLang["code"].Value,
						file = null,
						fileContent = fileContent
					});
				}
				addedDefinitions = true;
			}
			//
			var language = Languages[index];
			if (language.file == null && string.IsNullOrWhiteSpace(language.fileContent)) {
				Debug.LogWarning("No definitions for localisation of " + language.language);
			}
			else {
				var json = SimpleJSON.JSONNode.Parse(language.file == null ? language.fileContent : language.file.text);
				if (json.IsObject) {
					GetLocalisationKeys(new string[keyPartCount], 0, json);
				}
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
			Inst.InitTranslations(index);
			CurLanguageIndex = index;
			Inst.LocaliseAll();
			LANGUAGE_CHANGED.Broadcast();
		}

		//

		public static void Register(ILocaliseComponent component) {
			if (localisations.Contains(component)) { Debug.LogWarning("Trying to add ui to localisation more than once!"); return; }
			localisations.Add(component);
			if (Inst != null) {
				component.Localise();
			}
		}

		public static void Unregister(ILocaliseComponent loca) {
#if UNITY_EDITOR
			if (!localisations.Contains(loca)) { Debug.LogWarning("Trying to unregister ui from localisation without it being registered!"); return; }
#endif
			localisations.Remove(loca);
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

		//

		void LocaliseAll() {
			foreach (var loca in localisations) {
				loca.Localise();
			}
		}
	}

}