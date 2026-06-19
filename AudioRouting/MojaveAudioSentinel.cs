// ============================================================================
// MOJAVE AUDIO SENTINEL (SMART-HUB DAEMON)
// Tier 2: Zero-Latency WASAPI Audio Relay & Hot-Swapper
// Target: .NET 10 | Dependency: NAudio
// ============================================================================

using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace f76World.AudioRouting
{
    /// <summary>
    /// Represents an available physical audio endpoint in the system.
    /// </summary>
    public class PhysicalAudioDevice
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// The core relay engine. Captures from the Static Virtual Cable and 
    /// streams to the volatile Physical Audio Endpoint.
    /// </summary>
    public class MojaveAudioSentinel : IDisposable
    {
        private const string VIRTUAL_CABLE_NAME = "f76.world Game Audio"; // Must match deployed virtual cable name

        private WasapiLoopbackCapture? _captureNode;
        private WasapiOut? _renderNode;
        private BufferedWaveProvider? _ringBuffer;

        private readonly MMDeviceEnumerator _deviceEnumerator;
        public string CurrentPhysicalEndpointId { get; private set; } = string.Empty;
        public bool IsRelayActive { get; private set; }

        public event Action<string>? OnError;
        public event Action? OnRelayStateChanged;

        public MojaveAudioSentinel()
        {
            _deviceEnumerator = new MMDeviceEnumerator();
        }

        /// <summary>
        /// Scans the system for all active audio playback devices, excluding our virtual cables.
        /// </summary>
        public List<PhysicalAudioDevice> GetAvailablePhysicalDevices()
        {
            var devices = new List<PhysicalAudioDevice>();
            try
            {
                var endpoints = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                foreach (var endpoint in endpoints)
                {
                    // Filter out the virtual cable so we don't create an infinite audio loop
                    if (!endpoint.FriendlyName.Contains(VIRTUAL_CABLE_NAME, StringComparison.OrdinalIgnoreCase))
                    {
                        devices.Add(new PhysicalAudioDevice
                        {
                            Id = endpoint.ID,
                            Name = endpoint.FriendlyName,
                            IsActive = endpoint.ID == CurrentPhysicalEndpointId
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"Failed to enumerate audio devices: {ex.Message}");
            }
            return devices;
        }

        /// <summary>
        /// Executes the hot-swap routing sequence. 
        /// </summary>
        public void RouteAudioToDevice(string targetDeviceId)
        {
            try
            {
                StopRelay(); // Tear down existing pipes

                var virtualEndpoint = GetVirtualCableEndpoint();
                if (virtualEndpoint == null)
                {
                    OnError?.Invoke($"Virtual Cable '{VIRTUAL_CABLE_NAME}' not found. Is the driver installed?");
                    return;
                }

                var physicalEndpoint = _deviceEnumerator.GetDevice(targetDeviceId);
                if (physicalEndpoint == null || physicalEndpoint.State != DeviceState.Active)
                {
                    OnError?.Invoke("Selected physical device is disconnected or unavailable.");
                    return;
                }

                // 1. Initialize Capture (Listening to the Virtual Cable)
                _captureNode = new WasapiLoopbackCapture(virtualEndpoint);

                // 2. Initialize the Ring Buffer (Zero-latency RAM pipeline)
                _ringBuffer = new BufferedWaveProvider(_captureNode.WaveFormat)
                {
                    BufferDuration = TimeSpan.FromMilliseconds(50), // Ultra-low latency buffer
                    DiscardOnBufferOverflow = true
                };

                _captureNode.DataAvailable += (s, a) =>
                {
                    _ringBuffer.AddSamples(a.Buffer, 0, a.BytesRecorded);
                };

                // 3. Initialize Render (Playing to the Physical Headset/Speakers)
                _renderNode = new WasapiOut(physicalEndpoint, AudioClientShareMode.Shared, false, 20);
                _renderNode.Init(_ringBuffer);

                // 4. Ignite the Engine
                _renderNode.Play();
                _captureNode.StartRecording();

                CurrentPhysicalEndpointId = targetDeviceId;
                IsRelayActive = true;
                OnRelayStateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                IsRelayActive = false;
                OnError?.Invoke($"Relay routing failed: {ex.Message}");
                OnRelayStateChanged?.Invoke();
            }
        }

        /// <summary>
        /// Restarts the audio engine on the currently selected device.
        /// </summary>
        public void RestartRelay()
        {
            if (string.IsNullOrEmpty(CurrentPhysicalEndpointId)) return;
            RouteAudioToDevice(CurrentPhysicalEndpointId);
        }

        public void StopRelay()
        {
            IsRelayActive = false;

            if (_captureNode != null)
            {
                _captureNode.StopRecording();
                _captureNode.Dispose();
                _captureNode = null;
            }

            if (_renderNode != null)
            {
                _renderNode.Stop();
                _renderNode.Dispose();
                _renderNode = null;
            }

            _ringBuffer?.ClearBuffer();
            OnRelayStateChanged?.Invoke();
        }

        private MMDevice? GetVirtualCableEndpoint()
        {
            var endpoints = _deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            return endpoints.FirstOrDefault(e => e.FriendlyName.Contains(VIRTUAL_CABLE_NAME, StringComparison.OrdinalIgnoreCase));
        }

        public void Dispose()
        {
            StopRelay();
            _deviceEnumerator.Dispose();
        }
    }
}