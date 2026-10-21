using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace NotMyBlood.Core
{
    /// <summary>
    /// Steuert den Strom im Haus (Konzept 1.1):
    /// - Sabotage am Sicherungskasten: Licht geht nach 20 s aus, 15 s Dunkelheit, 1x per Runde (nur in Ermittlung)
    /// - Zufälliger Stromausfall: 50 % Chance, irgendwann zwischen Minute 1 und 4 der Ermittlung
    /// </summary>
    public class PowerManager : MonoBehaviour
    {
        public static PowerManager Instance { get; private set; }

        [Header("Power Settings")]
        [SerializeField] private GameObject lightsGroup;
        [SerializeField] private float sabotageDelay = 20f;
        [SerializeField] private float blackoutDuration = 15f;
        [Range(0f, 1f)]
        [SerializeField] private float randomBlackoutChance = 0.5f;
        [SerializeField] private Vector2 randomBlackoutWindow = new Vector2(60f, 240f);

        [Header("Dunkelheit")]
        [Range(0f, 1f)]
        [SerializeField] private float ambientDimFactor = 0.08f;
        [SerializeField] private Light[] extraLightsToDim = new Light[0];
        [SerializeField] private float fadeOutTime = 0.15f;
        [SerializeField] private float fadeInTime = 0.6f;

        public bool IsPowerOut { get; private set; }
        public bool WasSabotaged { get; private set; }

        private float blackoutEndTime;
        private Coroutine blackoutRoutine;
        private Coroutine scheduledBlackoutRoutine;

        private bool originalsSaved;
        private SphericalHarmonicsL2 originalAmbientProbe;
        private float originalAmbientIntensity;
        private Color originalSkyColor, originalEquatorColor, originalGroundColor;
        private float originalReflectionIntensity;
        private float[] originalExtraIntensities;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDisable()
        {
            // Nie mit gedimmter Szene zurückbleiben (z. B. Szenenwechsel mitten im Stromausfall)
            if (IsPowerOut) RestorePower();
        }

        private void Start()
        {
            if (lightsGroup == null)
            {
                lightsGroup = GameObject.Find("Lichter");
            }

            SaveOriginalLighting();

            // Abo erst in Start: dann existiert GameRoundManager.Instance sicher
            if (GameRoundManager.Instance != null)
            {
                GameRoundManager.Instance.PhaseChanged += OnPhaseChanged;
            }

            // Falls das Spiel direkt in der Ermittlung startet
            if (GameRoundManager.Instance != null && GameRoundManager.Instance.CurrentPhase == GamePhase.Ermittlung)
            {
                ScheduleRandomBlackout();
            }
        }

        private void OnDestroy()
        {
            if (GameRoundManager.Instance != null)
            {
                GameRoundManager.Instance.PhaseChanged -= OnPhaseChanged;
            }
            if (Instance == this) Instance = null;
        }

        private void OnPhaseChanged(GamePhase newPhase)
        {
            // Zufälligen Stromausfall erst planen, wenn die Ermittlungsphase beginnt
            if (newPhase == GamePhase.Ermittlung)
            {
                ScheduleRandomBlackout();
            }
        }

        // ------------------------------------------------------------------ Auslöser

        /// <summary>Wird aufgerufen, wenn der Mörder den Sicherungskasten manipuliert (nur in Ermittlung).</summary>
        public void SabotagePower()
        {
            if (GameRoundManager.Instance != null && GameRoundManager.Instance.CurrentPhase != GamePhase.Ermittlung)
            {
                Debug.Log("[PowerManager] Sabotage ist außerhalb der Ermittlungsphase nicht erlaubt!");
                return;
            }

            if (WasSabotaged)
            {
                Debug.Log("[PowerManager] Stromkasten wurde in dieser Runde bereits manipuliert!");
                return;
            }

            WasSabotaged = true;
            Debug.Log($"[PowerManager] Stromkasten manipuliert! Licht geht in {sabotageDelay} Sekunden aus...");
            StartCoroutine(BlackoutAfterDelay(sabotageDelay));
        }

        public void TriggerBlackout(float duration)
        {
            blackoutEndTime = Mathf.Max(blackoutEndTime, Time.time + duration);
            if (blackoutRoutine == null)
            {
                blackoutRoutine = StartCoroutine(BlackoutRoutine());
            }
        }

        private void ScheduleRandomBlackout()
        {
            if (UnityEngine.Random.value > randomBlackoutChance) return;

            float time = UnityEngine.Random.Range(randomBlackoutWindow.x, randomBlackoutWindow.y);
            Debug.Log($"[PowerManager] Zufälliger Stromausfall für diese Runde geplant in {time:F1} Sekunden.");
            
            if (scheduledBlackoutRoutine != null) StopCoroutine(scheduledBlackoutRoutine);
            scheduledBlackoutRoutine = StartCoroutine(BlackoutAfterDelay(time));
        }

        private IEnumerator BlackoutAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            TriggerBlackout(blackoutDuration);
        }

        // ------------------------------------------------------------------ Ablauf

        private IEnumerator BlackoutRoutine()
        {
            IsPowerOut = true;
            SetRoomLights(false);
            Debug.Log("[PowerManager] 💡 Lichter AUS!");
            yield return FadeDarkness(0f, 1f, fadeOutTime);

            while (Time.time < blackoutEndTime)
            {
                yield return null;
            }

            SetRoomLights(true);
            yield return FadeDarkness(1f, 0f, fadeInTime);
            Debug.Log("[PowerManager] 💡 Lichter wieder AN!");

            IsPowerOut = false;
            blackoutRoutine = null;
        }

        private IEnumerator FadeDarkness(float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                ApplyDarkness(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            ApplyDarkness(to);
        }

        private void RestorePower()
        {
            if (blackoutRoutine != null) StopCoroutine(blackoutRoutine);
            blackoutRoutine = null;
            SetRoomLights(true);
            ApplyDarkness(0f);
            IsPowerOut = false;
        }

        private void SetRoomLights(bool on)
        {
            if (lightsGroup != null) lightsGroup.SetActive(on);
        }

        // ------------------------------------------------------------------ Umgebungslicht

        private void SaveOriginalLighting()
        {
            originalAmbientProbe = RenderSettings.ambientProbe;
            originalAmbientIntensity = RenderSettings.ambientIntensity;
            originalSkyColor = RenderSettings.ambientSkyColor;
            originalEquatorColor = RenderSettings.ambientEquatorColor;
            originalGroundColor = RenderSettings.ambientGroundColor;
            originalReflectionIntensity = RenderSettings.reflectionIntensity;

            if ((extraLightsToDim == null || extraLightsToDim.Length == 0) && RenderSettings.sun != null)
            {
                extraLightsToDim = new[] { RenderSettings.sun };
            }
            extraLightsToDim ??= new Light[0];
            originalExtraIntensities = new float[extraLightsToDim.Length];
            for (int i = 0; i < extraLightsToDim.Length; i++)
            {
                originalExtraIntensities[i] = extraLightsToDim[i] != null ? extraLightsToDim[i].intensity : 0f;
            }

            originalsSaved = true;
        }

        /// <summary>darkness: 0 = normal, 1 = volle Dunkelheit.</summary>
        private void ApplyDarkness(float darkness)
        {
            if (!originalsSaved) return;

            float factor = Mathf.Lerp(1f, ambientDimFactor, darkness);

            // Skybox-Modus (Standard): URP liest das Umgebungslicht aus der Ambient-Probe – NICHT entfernen!
            RenderSettings.ambientIntensity = originalAmbientIntensity * factor;
            RenderSettings.ambientSkyColor = Scale(originalSkyColor, factor);
            RenderSettings.ambientEquatorColor = Scale(originalEquatorColor, factor);
            RenderSettings.ambientGroundColor = Scale(originalGroundColor, factor);
            RenderSettings.reflectionIntensity = originalReflectionIntensity * factor;

            for (int i = 0; i < extraLightsToDim.Length; i++)
            {
                if (extraLightsToDim[i] != null) 
                {
                    extraLightsToDim[i].intensity = originalExtraIntensities[i] * factor;
                }
            }
        }

        private static Color Scale(Color color, float factor) =>
            new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
    }
}