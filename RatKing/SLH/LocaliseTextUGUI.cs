using UnityEngine;

namespace RatKing.SLH {

	public class LocaliseTextUGUI : MonoBehaviour {
		[SerializeField] string key = "";
		[SerializeField, Tooltip("If index is -1, the index is chosen randomly")] int index = 0;
		[SerializeField] bool convertSpecial = true;
		[SerializeField] bool includeChildren = false;
		
		UnityEngine.UI.Text[] texts;

		void OnValidate() {
			if (index < -1) { index = -1; }
		}

		//

		void Awake() {
			Initialise();
		}

		void Initialise() {
			texts = includeChildren ? GetComponentsInChildren<UnityEngine.UI.Text>(true) : GetComponents<UnityEngine.UI.Text>();
			if (texts.Length == 0) { Debug.LogWarning("Trying to localise empty ui [" + key + "]"); Destroy(this); return; }
			Localisation.RegisterCallback(Localise);
		}

		void OnDestroy() {
			if (texts != null) { Localisation.UnregisterCallback(Localise); }
		}

		//

		void Localise() {
			if (texts == null) { Initialise(); return; }
			var text = index < 0 ? Localisation.Do(key, true, convertSpecial) : Localisation.Do(key, index, convertSpecial);
			foreach (var t in texts) { t.text = text; }
		}
	}

}
