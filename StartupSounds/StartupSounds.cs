using FrooxEngine;
using HarmonyLib;
using ManagedBass;
using ResoniteModLoader;
using System;
using System.IO;
using System.Linq;

namespace StartupSounds;

public class StartupSounds : ResoniteMod
{
    public override string Name => "StartupSounds";
    public override string Author => "dfgHiatus";
    public override string Version => "2.0.0";
    public override string Link => "https://github.com/dfgHiatus/StartupSounds";

    private static ModConfiguration? config;
    private static int audioStream;

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<string> soundDir = 
        new("soundDir", "Optional startup sounds directory. Leave empty to use the default \"startup_sounds\" folder", () => string.Empty);

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<string> soundfile = 
        new("soundfile", "The sound file to be played (only used if random is false)", () => "WaterLily.ogg");

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<bool> random = 
        new("random", "Play a random song on startup", () => true);

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<string> initSoundDir =
        new("initSoundDir", "Directory for initialization sounds", () => "init_sounds");

    [AutoRegisterConfigKey]
    public static readonly ModConfigurationKey<string> readySoundDir =
        new("readySoundDir", "Directory for ready state sounds", () => "ready_sounds");

    private static readonly string defaultStartupSoundsPath = Path.Combine("rml_mods", "startup_sounds");
    private static readonly string defaultInitSoundsPath = Path.Combine(defaultStartupSoundsPath, "init_sounds");
    private static readonly string defaultReadySoundsPath = Path.Combine(defaultStartupSoundsPath, "ready_sounds");

    private bool bassInitialized = false;

    public override void OnEngineInit()
    {
        // Initialize Bass first
        if (!Bass.Init())
        {
            Error("Bass failed to initialize! " + Bass.LastError);
            Error("This is likely due to the fact that you are missing the bass.dll and/or ManagedBass.dll file in your base directory. Please ensure they are there!");
            return;
        }
        bassInitialized = true;

        config = GetConfiguration();
        if (config == null)
        {
            Error("Failed to load configuration!");
            CleanupBass();
            return;
        }

        // Create nested directory structure
        Directory.CreateDirectory(defaultStartupSoundsPath);
        Directory.CreateDirectory(defaultInitSoundsPath);
        Directory.CreateDirectory(defaultReadySoundsPath);

        // Play initialization sound
        try
        {
            string initSound = GetRandomAudioFile(defaultInitSoundsPath);
            PlaySound(initSound);
        }
        catch (FileNotFoundException)
        {
            // If no init sound found, play from default startup sounds
            try
            {
                string startupSound = GetRandomAudioFile(defaultStartupSoundsPath);
                PlaySound(startupSound);
            }
            catch (FileNotFoundException ex)
            {
                Error($"Failed to load any audio files: {ex.Message}");
                CleanupBass();
                return;
            }
        }

        // Listen for engine ready state
        Engine.Current.OnReady += () =>
        {
            try
            {
                // Stop current sound
                StopCurrentSound();

                // Play ready sound
                string readySound = GetRandomAudioFile(defaultReadySoundsPath);
                PlaySound(readySound);
            }
            catch (FileNotFoundException)
            {
                // If no ready sound found, that's okay
                Msg("No ready sound found, continuing with current sound");
            }
        };

        new Harmony("net.dfgHiatus.StartupSounds").PatchAll();
    }

    private void PlaySound(string soundPath)
    {
        if (!bassInitialized) return;

        StopCurrentSound();

        audioStream = Bass.CreateStream(soundPath);
        if (audioStream == 0)
        {
            Error($"Failed to create audio stream: {Bass.LastError}");
            return;
        }
        Bass.ChannelPlay(audioStream, true);
    }

    private void StopCurrentSound()
    {
        if (audioStream != 0)
        {
            Bass.ChannelStop(audioStream);
            Bass.StreamFree(audioStream);
            audioStream = 0;
        }
    }

    private void CleanupBass()
    {
        StopCurrentSound();
        if (bassInitialized)
        {
            Bass.Free();
            bassInitialized = false;
        }
    }

    private static string GetRandomAudioFile(string dir)
    {           
        if (string.IsNullOrEmpty(dir))
        {
            throw new ArgumentException("Directory path cannot be empty!", nameof(dir));
        }

        string[] files = Directory.GetFiles(dir, "*.wav", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(dir, "*.flac", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(dir, "*.ogg", SearchOption.AllDirectories))
            .Concat(Directory.GetFiles(dir, "*.mp3", SearchOption.AllDirectories))
            .ToArray();
        
        if (files.Length == 0) 
        {
            throw new FileNotFoundException($"No audio files were found in the directory: {dir}");
        }

        return files[new Random().Next(0, files.Length)];
    }
}