using System;
using System.Collections.Generic;
using UnityEngine;
using NotMyBlood.Core;

namespace NotMyBlood.Player
{
    /// <summary>Eigene Einschätzung eines Mitspielers (Gedanken-Tracker im Notizbuch).</summary>
    public enum Suspicion { Unklar, Verdaechtig, Entlastet }

    /// <summary>Was der lokale Spieler über einen Mitspieler weiss.</summary>
    [Serializable]
    public class SuspectInfo
    {
        public PlayerIdentity player;
        public bool isSelf;
        public ClothingColor color;          // Kleidung sieht man immer
        public bool bloodKnown;
        public BloodType blood;
        public bool shoeKnown;
        public ShoeProfile shoe;
        public Suspicion status;

        public string Name => player != null ? player.PlayerName : "?";
    }

    /// <summary>
    /// Wissen des lokalen Spielers (wie der ClueManager, aber für Personen):
    /// Nadeltests (max. 2 pro Runde), Schuh-Fotos und der Gedanken-Tracker.
    /// Wird bei jeder neuen Runde automatisch zurückgesetzt.
    /// </summary>
    public class PlayerKnowledge : MonoBehaviour
    {
        public static PlayerKnowledge Instance { get; private set; }

        [SerializeField] private int needlesPerRound = 2;

        /// <summary>Etwas hat sich geändert (Notizbuch neu zeichnen).</summary>
        public event Action Changed;

        /// <summary>Kurze Meldung für das HUD, z. B. "Nadeltest Dummy 2: Blutgruppe A".</summary>
        public event Action<string> Feedback;

        public int NeedlesPerRound => needlesPerRound;

        public int NeedlesLeft
        {
            get { EnsureCurrentRound(); return needlesLeft; }
        }

        public IReadOnlyList<SuspectInfo> Suspects
        {
            get { EnsureCurrentRound(); return suspects; }
        }

        private readonly List<SuspectInfo> suspects = new List<SuspectInfo>();
        private int needlesLeft;
        private int roundSeen = -1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ Aktionen

        /// <summary>Nadeltest: kostet eine Nadel, verrät die Blutgruppe. Das Opfer merkt es.</summary>
        public void TryNeedleTest(PlayerIdentity target)
        {
            var info = Get(target);
            if (info == null || info.isSelf) return;

            if (info.bloodKnown)
            {
                Feedback?.Invoke($"Die Blutgruppe von {info.Name} kennst du schon");
                return;
            }
            if (needlesLeft <= 0)
            {
                Feedback?.Invoke("Keine Nadeln mehr in dieser Runde");
                return;
            }

            needlesLeft--;
            info.bloodKnown = true;
            info.blood = target.Traits.blood;
            target.NotifyPricked(FindLocalPlayer());

            Feedback?.Invoke($"Nadeltest {info.Name}: Blutgruppe {ClueData.BloodTypeText(info.blood)}");
            Changed?.Invoke();
        }

        /// <summary>Schuh-Foto: unbegrenzt, verrät das Schuhprofil. Rückgabe = Text fürs HUD.</summary>
        public string TakeShoePhoto(PlayerIdentity target)
        {
            var info = Get(target);
            if (info == null || info.isSelf) return null;

            info.shoeKnown = true;
            info.shoe = target.Traits.shoe;
            Changed?.Invoke();
            return $"Foto {info.Name}: Schuhprofil {info.shoe}";
        }

        /// <summary>Gedanken-Tracker: Unklar → Verdächtig → Entlastet → Unklar.</summary>
        public void CycleStatus(PlayerIdentity target)
        {
            var info = Get(target);
            if (info == null || info.isSelf) return;
            info.status = info.status switch
            {
                Suspicion.Unklar => Suspicion.Verdaechtig,
                Suspicion.Verdaechtig => Suspicion.Entlastet,
                _ => Suspicion.Unklar,
            };
            Changed?.Invoke();
        }

        public SuspectInfo Get(PlayerIdentity player)
        {
            if (player == null) return null;
            EnsureCurrentRound();
            foreach (var info in suspects)
            {
                if (info.player == player) return info;
            }
            return null;
        }

        // ------------------------------------------------------------------ Runde

        private void EnsureCurrentRound()
        {
            var round = GameRoundManager.Instance;
            int roundNumber = round != null ? round.RoundNumber : 0;
            if (roundNumber == roundSeen) return;
            roundSeen = roundNumber;
            Rebuild(round);
        }

        private void Rebuild(GameRoundManager round)
        {
            suspects.Clear();
            needlesLeft = needlesPerRound;

            IEnumerable<PlayerIdentity> players = round != null
                ? round.Players
                : FindObjectsByType<PlayerIdentity>();

            foreach (var player in players)
            {
                if (player == null) continue;
                bool self = player.IsLocal;
                var info = new SuspectInfo
                {
                    player = player,
                    isSelf = self,
                    color = player.Traits.color,
                    bloodKnown = self,          // die eigene Blutgruppe kennt man
                    blood = player.Traits.blood,
                    shoeKnown = self,
                    shoe = player.Traits.shoe,
                };
                if (self) suspects.Insert(0, info);
                else suspects.Add(info);
            }

            // Werte von Unbekannten nicht "vorab" speichern
            foreach (var info in suspects)
            {
                if (!info.bloodKnown) info.blood = default;
                if (!info.shoeKnown) info.shoe = default;
            }

            Changed?.Invoke();
        }

        private PlayerIdentity FindLocalPlayer()
        {
            foreach (var info in suspects)
            {
                if (info.isSelf) return info.player;
            }
            return null;
        }
    }
}
