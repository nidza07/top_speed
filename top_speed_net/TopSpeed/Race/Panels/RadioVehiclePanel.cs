using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using TopSpeed.Audio;
using TopSpeed.Core;
using TopSpeed.Core.Settings;
using TopSpeed.Input;
using TopSpeed.Vehicles;
using TS.Audio;

namespace TopSpeed.Race.Panels
{
    internal sealed class RadioVehiclePanel : IVehicleRacePanel
    {
        private const int VolumeStepPercent = 10;
        private static readonly string[] SupportedExtensions = { ".wav", ".ogg", ".mp3", ".flac", ".aac", ".m4a" };

        private readonly RaceInput _input;
        private readonly AudioManager _audio;
        private readonly RaceSettings _settings;
        private readonly VehicleRadioController _radio;
        private readonly Func<uint> _nextMediaId;
        private readonly Action<string> _announce;
        private readonly Action<uint, string>? _mediaLoaded;
        private readonly Action<bool, bool, uint>? _playbackChanged;
        private readonly object _pendingPathLock = new object();
        private readonly List<string> _playlist = new List<string>();
        private readonly Random _random = new Random();

        private volatile bool _pickerInProgress;
        private volatile bool _folderPickerInProgress;
        private string? _pendingSelectedPath;
        private string? _pendingSelectedFolder;
        private string _playlistFolder = string.Empty;
        private int _playlistIndex = -1;
        private bool _shuffleMode;
        private bool _loopMode;
        private bool _lastObservedPlaying;
        private AudioSourceHandle? _volumeUpSound;
        private AudioSourceHandle? _volumeDownSound;

        public RadioVehiclePanel(
            RaceInput input,
            AudioManager audio,
            RaceSettings settings,
            VehicleRadioController radio,
            Func<uint> nextMediaId,
            Action<string> announce,
            Action<uint, string>? mediaLoaded = null,
            Action<bool, bool, uint>? playbackChanged = null)
        {
            _input = input ?? throw new ArgumentNullException(nameof(input));
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _radio = radio ?? throw new ArgumentNullException(nameof(radio));
            _nextMediaId = nextMediaId ?? throw new ArgumentNullException(nameof(nextMediaId));
            _announce = announce ?? throw new ArgumentNullException(nameof(announce));
            _mediaLoaded = mediaLoaded;
            _playbackChanged = playbackChanged;
            _shuffleMode = _settings.RadioShuffle;
            _loopMode = false;
            TryRestoreFolderPlaylist();
            ApplyLoopMode();
        }

        public string Name => "Radio";
        public bool AllowsDrivingInput => false;
        public bool AllowsAuxiliaryInput => false;

        public void Tick(float elapsed)
        {
            ProcessPendingSelection();
            ProcessPendingFolderSelection();
            HandlePlaybackEndAdvance();
        }

        public void Update(float elapsed)
        {
            Tick(elapsed);

            if (_input.GetOpenRadioMediaRequest())
                OpenRadioMedia();

            if (_input.GetOpenRadioFolderRequest())
                OpenRadioFolder();

            if (_input.GetToggleRadioPlaybackRequest())
                TogglePlayback();

            if (_input.GetRadioNextTrackRequest())
                CycleTrack(1);
            else if (_input.GetRadioPreviousTrackRequest())
                CycleTrack(-1);

            if (_input.GetRadioToggleShuffleRequest())
                ToggleShuffle();

            if (_input.GetRadioToggleLoopRequest())
                ToggleLoop();

            if (_input.GetRadioVolumeUpRequest())
                AdjustVolume(VolumeStepPercent, "volume_up.ogg");
            else if (_input.GetRadioVolumeDownRequest())
                AdjustVolume(-VolumeStepPercent, "volume_down.ogg");
        }

        public void Pause()
        {
            _radio.PauseForGame();
        }

        public void Resume()
        {
            _radio.ResumeFromGame();
        }

        public void Dispose()
        {
            _audio.ReleaseCachedSource(_volumeUpSound);
            _audio.ReleaseCachedSource(_volumeDownSound);
            _volumeUpSound = null;
            _volumeDownSound = null;
        }

        private void OpenRadioMedia()
        {
            if (_pickerInProgress)
                return;

            _pickerInProgress = true;
            BeginShowMediaPickerDialog(selectedPath =>
            {
                lock (_pendingPathLock)
                    _pendingSelectedPath = selectedPath;

                _pickerInProgress = false;
            });
        }

        private void OpenRadioFolder()
        {
            if (_folderPickerInProgress)
                return;

            _folderPickerInProgress = true;
            BeginShowFolderPickerDialog(_playlistFolder, selectedFolder =>
            {
                lock (_pendingPathLock)
                    _pendingSelectedFolder = selectedFolder;

                _folderPickerInProgress = false;
            });
        }

        private void ProcessPendingSelection()
        {
            string? selectedPath;
            lock (_pendingPathLock)
            {
                selectedPath = _pendingSelectedPath;
                _pendingSelectedPath = null;
            }

            if (string.IsNullOrWhiteSpace(selectedPath))
                return;

            var fullPath = Path.GetFullPath(selectedPath);
            if (!File.Exists(fullPath))
            {
                _announce("The selected media file does not exist.");
                return;
            }

            _playlist.Clear();
            _playlist.Add(fullPath);
            _playlistIndex = 0;
            _playlistFolder = string.Empty;
            ApplyLoopMode();

            LoadPlaylistEntry(_playlistIndex, preservePlaybackState: true, announceLoaded: true);
        }

        private void ProcessPendingFolderSelection()
        {
            string? selectedFolder;
            lock (_pendingPathLock)
            {
                selectedFolder = _pendingSelectedFolder;
                _pendingSelectedFolder = null;
            }

            if (string.IsNullOrWhiteSpace(selectedFolder))
                return;

            var folderPath = selectedFolder!;
            if (!BuildPlaylistFromFolder(folderPath, preserveCurrentMedia: false, announceErrors: true))
                return;

            if (!LoadPlaylistEntry(_playlistIndex, preservePlaybackState: true, announceLoaded: true))
                return;

            _announce($"Shuffle mode {(_shuffleMode ? "on" : "off")}.");
        }

        private void HandlePlaybackEndAdvance()
        {
            var isPlaying = _radio.HasMedia && _radio.IsPlaying;
            if (_radio.HasMedia && _radio.DesiredPlaying && _lastObservedPlaying && !isPlaying)
            {
                if (_playlist.Count > 1 && !_radio.LoopPlayback)
                {
                    if (StepPlaylistIndex(1))
                        LoadPlaylistEntry(_playlistIndex, preservePlaybackState: true, announceLoaded: true);
                }
            }

            _lastObservedPlaying = isPlaying;
        }

        private static void BeginShowMediaPickerDialog(Action<string?> onCompleted)
        {
            void ShowDialog()
            {
                string? selectedPath = null;
                using (var dialog = new OpenFileDialog())
                {
                    dialog.CheckFileExists = true;
                    dialog.CheckPathExists = true;
                    dialog.Multiselect = false;
                    dialog.Title = "Select radio media file";
                    dialog.Filter = "Audio files|*.wav;*.ogg;*.mp3;*.flac;*.aac;*.m4a|All files|*.*";

                    var owner = GetDialogOwner();
                    var result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
                    if (result == DialogResult.OK)
                        selectedPath = dialog.FileName;
                }

                onCompleted(selectedPath);
            }

            var ownerWindow = GetDialogOwnerForm();
            if (ownerWindow != null && ownerWindow.IsHandleCreated && !ownerWindow.IsDisposed)
            {
                ownerWindow.BeginInvoke((Action)ShowDialog);
                return;
            }

            var thread = new Thread(() => ShowDialog())
            {
                IsBackground = true,
                Name = "RadioMediaPicker"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private static void BeginShowFolderPickerDialog(string currentFolder, Action<string?> onCompleted)
        {
            void ShowDialog()
            {
                string? selectedFolder = null;
                using (var dialog = new FolderBrowserDialog())
                {
                    dialog.Description = "Select radio media folder";
                    dialog.ShowNewFolderButton = false;
                    if (!string.IsNullOrWhiteSpace(currentFolder) && Directory.Exists(currentFolder))
                        dialog.SelectedPath = currentFolder;

                    var owner = GetDialogOwner();
                    var result = owner != null ? dialog.ShowDialog(owner) : dialog.ShowDialog();
                    if (result == DialogResult.OK)
                        selectedFolder = dialog.SelectedPath;
                }

                onCompleted(selectedFolder);
            }

            var ownerWindow = GetDialogOwnerForm();
            if (ownerWindow != null && ownerWindow.IsHandleCreated && !ownerWindow.IsDisposed)
            {
                ownerWindow.BeginInvoke((Action)ShowDialog);
                return;
            }

            var thread = new Thread(() => ShowDialog())
            {
                IsBackground = true,
                Name = "RadioFolderPicker"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private static Form? GetDialogOwnerForm()
        {
            if (Application.OpenForms.Count == 0)
                return null;

            return Application.OpenForms[0];
        }

        private static IWin32Window? GetDialogOwner() => GetDialogOwnerForm();

        private void TogglePlayback()
        {
            if (!_radio.HasMedia)
            {
                if (_playlist.Count == 0)
                {
                    _announce("No radio media loaded.");
                    return;
                }

                SelectIndexFromCurrentMedia();
                if (_playlistIndex < 0)
                    _playlistIndex = 0;
                if (!LoadPlaylistEntry(_playlistIndex, preservePlaybackState: false, announceLoaded: true))
                    return;
                _radio.SetPlayback(true);
                _announce("Radio playing.");
                _playbackChanged?.Invoke(_radio.HasMedia, _radio.DesiredPlaying, _radio.MediaId);
                return;
            }

            _radio.TogglePlayback();
            _announce(_radio.DesiredPlaying ? "Radio playing." : "Radio paused.");
            _playbackChanged?.Invoke(_radio.HasMedia, _radio.DesiredPlaying, _radio.MediaId);
        }

        private void CycleTrack(int delta)
        {
            if (_playlist.Count == 0)
            {
                _announce("No folder playlist loaded.");
                return;
            }

            SelectIndexFromCurrentMedia();
            if (!StepPlaylistIndex(delta))
                return;

            LoadPlaylistEntry(_playlistIndex, preservePlaybackState: true, announceLoaded: true);
        }

        private void ToggleShuffle()
        {
            _shuffleMode = !_shuffleMode;
            _settings.RadioShuffle = _shuffleMode;
            SaveRadioSettings();

            var lastFolder = _settings.RadioLastFolder ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(lastFolder))
                BuildPlaylistFromFolder(lastFolder, preserveCurrentMedia: true, announceErrors: false);

            _announce($"Shuffle mode {(_shuffleMode ? "on" : "off")}.");
        }

        private void ToggleLoop()
        {
            _loopMode = !_loopMode;
            ApplyLoopMode();
            _announce($"Loop mode {(_loopMode ? "on" : "off")}.");
        }

        private bool BuildPlaylistFromFolder(string folderPath, bool preserveCurrentMedia, bool announceErrors)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                if (announceErrors)
                    _announce("No folder was selected.");
                return false;
            }

            string fullFolder;
            try
            {
                fullFolder = Path.GetFullPath(folderPath);
            }
            catch
            {
                if (announceErrors)
                    _announce("The selected folder path is invalid.");
                return false;
            }

            if (!Directory.Exists(fullFolder))
            {
                if (announceErrors)
                    _announce("The selected folder does not exist.");
                return false;
            }

            List<string> files;
            try
            {
                files = Directory
                    .EnumerateFiles(fullFolder, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(IsSupportedAudioFile)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                if (announceErrors)
                    _announce("Could not read files from the selected folder.");
                return false;
            }

            if (files.Count == 0)
            {
                if (announceErrors)
                    _announce("No supported audio files were found in the selected folder.");
                return false;
            }

            if (_shuffleMode)
                Shuffle(files);

            var currentPath = preserveCurrentMedia ? _radio.MediaPath : null;

            _playlist.Clear();
            _playlist.AddRange(files);
            _playlistFolder = fullFolder;
            _playlistIndex = 0;
            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                var idx = _playlist.FindIndex(path => string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                    _playlistIndex = idx;
            }

            _settings.RadioLastFolder = _playlistFolder;
            _settings.RadioShuffle = _shuffleMode;
            SaveRadioSettings();
            ApplyLoopMode();
            return true;
        }

        private static bool IsSupportedAudioFile(string path)
        {
            var extension = Path.GetExtension(path);
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            for (var i = 0; i < SupportedExtensions.Length; i++)
            {
                if (string.Equals(extension, SupportedExtensions[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void Shuffle(List<string> files)
        {
            for (var i = files.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                var tmp = files[i];
                files[i] = files[j];
                files[j] = tmp;
            }
        }

        private bool LoadPlaylistEntry(int index, bool preservePlaybackState, bool announceLoaded, bool announceNameOnly = false)
        {
            if (index < 0 || index >= _playlist.Count)
                return false;

            var mediaPath = _playlist[index];
            _playlistIndex = index;
            ApplyLoopMode();

            var mediaId = _nextMediaId();
            if (!_radio.TryLoadFromFile(mediaPath, mediaId, preservePlaybackState, out var error))
            {
                _announce($"Failed to load radio media. {error}");
                return false;
            }

            if (announceLoaded)
            {
                var fileName = Path.GetFileName(mediaPath);
                if (announceNameOnly)
                    _announce(fileName);
                else
                    _announce($"Radio loaded {fileName}.");
            }

            _mediaLoaded?.Invoke(mediaId, mediaPath);
            _playbackChanged?.Invoke(_radio.HasMedia, _radio.DesiredPlaying, _radio.MediaId);
            _lastObservedPlaying = _radio.HasMedia && _radio.IsPlaying;
            return true;
        }

        private void SelectIndexFromCurrentMedia()
        {
            if (_playlist.Count == 0)
            {
                _playlistIndex = -1;
                return;
            }

            var currentPath = _radio.MediaPath;
            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                var idx = _playlist.FindIndex(path => string.Equals(path, currentPath, StringComparison.OrdinalIgnoreCase));
                if (idx >= 0)
                {
                    _playlistIndex = idx;
                    return;
                }
            }

            if (_playlistIndex < 0 || _playlistIndex >= _playlist.Count)
                _playlistIndex = 0;
        }

        private bool StepPlaylistIndex(int delta)
        {
            if (_playlist.Count == 0)
                return false;

            if (_playlistIndex < 0 || _playlistIndex >= _playlist.Count)
                _playlistIndex = 0;
            else
                _playlistIndex += delta;

            while (_playlistIndex < 0)
                _playlistIndex += _playlist.Count;
            while (_playlistIndex >= _playlist.Count)
                _playlistIndex -= _playlist.Count;

            return true;
        }

        private void ApplyLoopMode()
        {
            _radio.SetLoopPlayback(_loopMode || _playlist.Count <= 1);
        }

        private void TryRestoreFolderPlaylist()
        {
            if (string.IsNullOrWhiteSpace(_settings.RadioLastFolder))
                return;

            BuildPlaylistFromFolder(_settings.RadioLastFolder, preserveCurrentMedia: false, announceErrors: false);
        }

        private void SaveRadioSettings()
        {
            try
            {
                new SettingsManager().Save(_settings);
            }
            catch
            {
            }
        }

        private void AdjustVolume(int deltaPercent, string feedbackSound)
        {
            var previous = _radio.VolumePercent;
            var target = previous + deltaPercent;
            if (target < 0)
                target = 0;
            else if (target > 100)
                target = 100;

            _radio.SetVolumePercent(target);
            if (target != previous)
                _announce($"{target}%");
            PlayFeedback(feedbackSound);
        }

        private void PlayFeedback(string fileName)
        {
            var sound = GetFeedbackSound(fileName);
            if (sound == null)
                return;

            try
            {
                sound.SetVolumePercent(_settings, AudioVolumeCategory.OnlineServerEvents, 100);
                sound.Restart(loop: false);
            }
            catch
            {
            }
        }

        private AudioSourceHandle? GetFeedbackSound(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            ref var cache = ref _volumeDownSound;
            if (string.Equals(fileName, "volume_up.ogg", StringComparison.OrdinalIgnoreCase))
                cache = ref _volumeUpSound;

            if (cache != null)
                return cache;

            var path = Path.Combine(AssetPaths.SoundsRoot, "network", fileName);
            if (!_audio.TryResolvePath(path, out var fullPath))
                return null;

            try
            {
                cache = _audio.AcquireCachedSource(fullPath, streamFromDisk: false);
                cache.SetVolumePercent(_settings, AudioVolumeCategory.OnlineServerEvents, 100);
                return cache;
            }
            catch
            {
                return null;
            }
        }
    }
}
