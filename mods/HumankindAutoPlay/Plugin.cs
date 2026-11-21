using System;
using System.IO;
using Amplitude.Mercury.AI;
using BepInEx;
using BepInEx.Configuration;
using HumankindMods.Core;
using UnityEngine;

namespace HumankindAutoPlay
{
    /// <summary>
    /// Hands every empire to the AI and ends turns automatically until a target turn, the same three
    /// preferences the game's developer Auto Turn window sets (FloatingWindow_AutoTurn.cs:208-220).
    /// Inactive unless BepInEx/AutoPlay/request.txt exists.
    /// </summary>
    [BepInPlugin("com.zkidz.humankind.autoplay", "Humankind AutoPlay", "0.1.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        private ConfigEntry<KeyboardShortcut> stopKey = null!;
        private string requestPath = "";
        private string donePath = "";
        private LineLog log = null!;
        private HookGuard guard = null!;
        private int? targetTurn;
        private bool active;
        private int lastTurn = -1;
        private float lastTurnAt;
        private ControlledByHumanBinding savedBinding;
        private EmpireReadyOrderPolicy savedPolicy;

        private void Awake()
        {
            var dir = Path.Combine(Paths.BepInExRootPath, "AutoPlay");
            requestPath = Path.Combine(dir, "request.txt");
            donePath = Path.Combine(dir, "done.txt");
            log = new LineLog(Path.Combine(dir, "progress.log"));
            guard = new HookGuard(log.Write);
            stopKey = Config.Bind("AutoPlay", "StopKey", new KeyboardShortcut(KeyCode.F12, KeyCode.LeftControl), "Stops autoplay and hands control back.");

            targetTurn = File.Exists(requestPath) ? AutoPlayRequest.ParseTargetTurn(File.ReadAllText(requestPath)) : null;
            log.Write(targetTurn.HasValue ? "armed, target turn " + targetTurn : "idle (no request.txt)");
            Logger.LogInfo("Humankind AutoPlay loaded, " + (targetTurn.HasValue ? "target turn " + targetTurn : "idle"));
        }

        private void Update()
        {
            if (!targetTurn.HasValue) return;
            guard.Run("AutoPlay.Update", () =>
            {
                if (active && stopKey.Value.IsDown())
                {
                    Stop("stopped by hotkey");
                    return;
                }

                var game = Amplitude.Mercury.Interop.AI.Snapshots.Game;
                if (game == null || !Amplitude.Mercury.Presentation.Presentation.HasBeenStarted) return;
                if (!active) Start(game.Turn);
                if (game.Turn != lastTurn) OnTurn(game.Turn);
            });
        }

        private void Start(int turn)
        {
            savedBinding = Preferences.ControlledByHumanBindingAfterStartup;
            savedPolicy = Preferences.EmpireReadyOrderPolicy;
            Preferences.EmpireReadyOrderPolicy = EmpireReadyOrderPolicy.AlwaysSendEmpireReadyOrder;
            Preferences.ControlledByHumanBindingAfterStartup = ControlledByHumanBinding.ActivateAIOnAllEmpires;
            Preferences.NumberOfTicksBeforeSendEmpireReadyOrder = 0;
            active = true;
            lastTurnAt = Time.realtimeSinceStartup;
            log.Write("autoplay on at turn " + turn + ", target " + targetTurn);
        }

        private void OnTurn(int turn)
        {
            var seconds = Time.realtimeSinceStartup - lastTurnAt;
            lastTurnAt = Time.realtimeSinceStartup;
            lastTurn = turn;
            log.Write($"turn {turn} ({seconds:0}s)");
            if (AutoPlayRequest.IsReached(turn, targetTurn!.Value))
            {
                File.WriteAllText(donePath, "turn " + turn + " at " + DateTime.Now.ToString("o"));
                Stop("target turn reached");
                UnityEngine.Application.Quit();
            }
        }

        // Deleting the request keeps a relaunch from resuming a run that was finished or cancelled.
        private void Stop(string reason)
        {
            Preferences.ControlledByHumanBindingAfterStartup = savedBinding;
            Preferences.EmpireReadyOrderPolicy = savedPolicy;
            active = false;
            targetTurn = null;
            File.Delete(requestPath);
            log.Write("autoplay off: " + reason);
        }
    }
}
