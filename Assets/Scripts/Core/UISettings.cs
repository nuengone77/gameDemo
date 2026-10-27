using System;
using UnityEngine;

namespace MagicGame
{
    [Serializable]
    public class UISettings
    {
        const string Key = "MagicGame.UISettings.v1";
        public float controlSize = 1f;
        public float controlOpacity = 0.65f;
        public float textSize = 1f;
        public bool reducedMotion;
        public float masterVolume = 0.9f;
        public float musicVolume = 0.7f;
        public float sfxVolume = 0.85f;
        public float uiVolume = 0.8f;

        public static UISettings Load()
        {
            UISettings settings;
            try { settings = JsonUtility.FromJson<UISettings>(PlayerPrefs.GetString(Key, "")); }
            catch (ArgumentException) { settings = null; }
            settings ??= new UISettings();
            settings.controlSize = Clamp(settings.controlSize, 0.8f, 1.2f, 1f);
            settings.controlOpacity = Clamp(settings.controlOpacity, 0.3f, 1f, 0.65f);
            settings.textSize = Clamp(settings.textSize, 1f, 1.5f, 1f);
            settings.masterVolume = Clamp(settings.masterVolume, 0f, 1f, 0.9f);
            settings.musicVolume = Clamp(settings.musicVolume, 0f, 1f, 0.7f);
            settings.sfxVolume = Clamp(settings.sfxVolume, 0f, 1f, 0.85f);
            settings.uiVolume = Clamp(settings.uiVolume, 0f, 1f, 0.8f);
            return settings;
        }

        static float Clamp(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }
    }
}
