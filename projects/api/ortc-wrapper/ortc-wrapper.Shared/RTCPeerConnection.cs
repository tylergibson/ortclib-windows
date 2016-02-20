﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ortc_winrt_api;
using Log = ortc_winrt_api.Log;
using Windows.Foundation;

namespace OrtcWrapper
{
    public class RTCPeerConnection
    {
        //private SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        private bool _closed = false;
        private string audioSSRCLabel = null;
        private string videoSSRCLabel = null;
        private string cnameSSRC = null;
        private UInt32 ssrcId;
        //private RTCIceGatherer _iceGatherer;
        //private RtcIceTransport _iceTransport;
        //private RtcDtlsTransport _dtlsTransport;
        private TaskCompletionSource<RTCSessionDescription> _capabilitiesTcs;
        private TaskCompletionSource<RTCSessionDescription> _capabilitiesFinalTcs;
        private TaskCompletionSource<RTCSessionDescription> _remoteCapabilitiesTcs;
        //private RtcIceRole _iceRole = RtcIceRole.Controlling;
        //private RTCSessionDescription _localCapabilities;
       // private RTCSessionDescription _localCapabilitiesFinal;
        //private RTCSessionDescription _remoteCapabilities;
        //private MediaStream _localStream;
        
        private RTCRtpSender _audioSender;
        private RTCRtpSender _videoSender;
        private RTCRtpReceiver _audioReceiver;
        private RTCRtpReceiver _videoReceiver;
        private MediaDevice _audioPlaybackDevice;

        private bool _installedIceEvents;

        private RTCIceGatherOptions options { get; set; }
        private RTCIceGatherer iceGatherer { get; set; }
        private RTCIceGatherer iceGathererRTCP   { get; set; }
        private RTCDtlsTransport dtlsTransport  { get; set; }

        private RTCIceTransport iceTransport { get; set; }

        private MediaStream _localStream;
        private MediaStream _remoteStream;

        static public void SetPreferredVideoCaptureFormat(int frame_width,
                                            int frame_height, int fps)
        {

        }

        public delegate void RTCPeerConnectionIceEventDelegate(RTCPeerConnectionIceEvent evt);
        public delegate void MediaStreamEventEventDelegate(MediaStreamEvent evt);
        public delegate void RTCPeerConnectionHealthStatsDelegate(RTCPeerConnectionHealthStats stats);

        public event RTCPeerConnectionIceEventDelegate _OnIceCandidate;
        public event RTCPeerConnectionIceEventDelegate OnIceCandidate
        {
            add
            {
                _OnIceCandidate += value;
                //iceGatherer.OnICEGathererLocalCandidate += this.RTCIceGatherer_onICEGathererLocalCandidate;
            }
            remove
            {
                _OnIceCandidate -= value;
                //iceGatherer.OnICEGathererLocalCandidate -= this.RTCIceGatherer_onICEGathererLocalCandidate;
            }
        }
        public event MediaStreamEventEventDelegate OnAddStream;
        public event MediaStreamEventEventDelegate OnRemoveStream;
        public event RTCPeerConnectionHealthStatsDelegate OnConnectionHealthStats;

        public RTCPeerConnection(RTCConfiguration configuration)
        {
            Logger.SetLogLevel(Log.Level.Trace);
            Logger.SetLogLevel(Log.Component.ZsLib, Log.Level.Trace);
            Logger.SetLogLevel(Log.Component.Services, Log.Level.Trace);
            Logger.SetLogLevel(Log.Component.ServicesHttp, Log.Level.Trace);
            Logger.SetLogLevel(Log.Component.OrtcLib, Log.Level.Insane);
            Logger.SetLogLevel("ortc_standup", Log.Level.Insane);


            //openpeer::services::ILogger::installDebuggerLogger();
            Logger.InstallTelnetLogger(59999, 60, true);

            Settings.ApplyDefaults();
            Ortc.Setup();

            //_installedIceEvents = false;

            options = new RTCIceGatherOptions();
            options.IceServers = new List<ortc_winrt_api.RTCIceServer>();

            foreach (RTCIceServer server in configuration.IceServers)
            {
                ortc_winrt_api.RTCIceServer ortcServer = new ortc_winrt_api.RTCIceServer();
                ortcServer.Urls = new List<string>();

                if (!string.IsNullOrEmpty(server.Credential))
                {
                    ortcServer.Credential = server.Credential;
                }

                if (!string.IsNullOrEmpty(server.Username))
                {
                    ortcServer.UserName = server.Username;
                }

                ortcServer.Urls.Add(server.Url);
                options.IceServers.Add(ortcServer);
            }

            PrepareGatherer();

            RTCCertificate.GenerateCertificate("").AsTask<RTCCertificate>().ContinueWith((cert) =>
            {
                //using (var @lock = new AutoLock(_lock))
                {
                    // Since the DTLS certificate is ready the RtcDtlsTransport can now be constructed.
                    var certs = new List<RTCCertificate>();
                    certs.Add(cert.Result);
                    dtlsTransport = new RTCDtlsTransport(iceTransport, certs);
                    if (_closed) dtlsTransport.Stop();
                }
            });

            sessionID = (ulong)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0).ToUniversalTime()).TotalMilliseconds;
        }

       
        /// <summary>
        /// Enable/Disable WebRTC statistics to ETW.
        /// </summary>
        public void ToggleETWStats(bool enable)
        {

        }

        /// <summary>
        /// Enable/Disable connection health statistics.
        /// When new connection health stats are available OnConnectionHealthStats
        //  event is raised.
        /// </summary>
        public void ToggleConnectionHealthStats(bool enable)
        {

        }

        /// <summary>
        /// Adds a new local <see cref="MediaStream"/> to be sent on this connection.
        /// </summary>
        /// <param name="stream"><see cref="MediaStream"/> to be added.</param>
        public void AddStream(MediaStream stream)
        {
            _localStream = stream;
        }

        public void Close()
        {

        }

        public Task SetRemoteDescription(RTCSessionDescription description)
        {
            return null;
        }

        public Task<RTCSessionDescription> CreateAnswer()//async
        {
            return null;
        }

        private UInt64 sessionID { get; set; }
        private UInt16 sessionVersion { get; set; }
        //private string streamID { get; set; }
        string createSDP()
        {
            StringBuilder sb = new StringBuilder();

            Boolean containsAudio = (_localStream.GetAudioTracks() != null) && _localStream.GetAudioTracks().Count > 0;
            Boolean containsVideo = (_localStream.GetVideoTracks() != null) && _localStream.GetVideoTracks().Count > 0;

            //------------- Global lines START -------------
            //v=0
            sb.Append("v=");
            sb.Append(0);
            sb.Append("\r\n");


            //o=- 1045717134763489491 2 IN IP4 127.0.0.1
            sb.Append("o=");
            sb.Append(sessionID);
            sb.Append(' ');
            sb.Append(sessionVersion);
            sb.Append(' ');
            sb.Append("IN");
            sb.Append(' ');
            sb.Append("IP4");
            sb.Append(' ');
            sb.Append("127.0.0.1");
            sb.Append("\r\n");

            //s=-
            sb.Append("s=");
            sb.Append("-");
            sb.Append("\r\n");

            //t=0 0
            sb.Append("t=");
            sb.Append(0);
            sb.Append(' ');
            sb.Append(0);
            sb.Append("\r\n");

            //a=group:BUNDLE audio video
            sb.Append("a=");
            sb.Append("group:BUNDLE");
            sb.Append(' ');
            if (containsAudio)
            {
                sb.Append("audio");
                sb.Append(' ');
            }
            if (containsVideo)
                sb.Append("video");

            sb.Append("\r\n");

            //a=msid-semantic: WMS stream_label_ce8753d3
            sb.Append("a=");
            sb.Append("msid-semantic:");
            sb.Append(' ');
            sb.Append("WMS");
            sb.Append(' ');
            sb.Append(_localStream.Id);
            sb.Append("\r\n");
            //------------- Global lines END -------------

            IList<MediaAudioTrack>  audioTracks = _localStream.GetAudioTracks();

            //------------- Media lines START -------------
            //m = audio 9 UDP / TLS / RTP / SAVPF 111 103 104 9 102 0 8 106 105 13 127 126
            if (containsAudio)
            {
                sb.Append("m=audio");
                sb.Append(' ');
                sb.Append('9');
                sb.Append("UDP/TLS/RTP/SAVPF");
                sb.Append(' ');
                var audioCapabilities = RTCRtpReceiver.GetCapabilities("audio");
                if (audioCapabilities != null)
                {
                    foreach (RTCRtpCodecCapability cap in audioCapabilities.Codecs)
                    {
                        sb.Append(cap.PreferredPayloadType);
                        sb.Append(' ');
                    }
                }
                sb.Append("\r\n");

                //c=IN IP4 0.0.0.0
                sb.Append("c=IN");
                sb.Append(' ');
                sb.Append("IP4");   //TODO get address type
                sb.Append(' ');
                sb.Append("0.0.0.0");
                sb.Append("\r\n");

                //a=rtcp:9 IN IP4 0.0.0.0
                sb.Append("a=");
                sb.Append("rtcp:");
                sb.Append('9');
                sb.Append(' ');
                sb.Append("IN");
                sb.Append(' ');
                sb.Append("IP4");   //TODO get address type
                sb.Append(' ');
                sb.Append("0.0.0.0");
                sb.Append("\r\n");

                RTCIceParameters iceParams = iceGatherer.GetLocalParameters();
                // a = ice - ufrag:C / tJt6mZEG7BQVUa
                sb.Append("a=ice-ufrag:");
                sb.Append(iceParams.UsernameFragment);
                sb.Append("\r\n");
                //a = ice - pwd:coliv0yBjagPtrltwt8u025S
                sb.Append("a=ice-pwd:");
                sb.Append(iceParams.Password);
                sb.Append("\r\n");

                RTCDtlsParameters dtlsParameters = dtlsTransport.GetLocalParameters();

                //a=fingerprint:sha-256 40:0C:E6:90:75:80:87:35:C7:F4:66:CB:C3:5E:F1:E8:63:55:12:09:0E:61:35:32:B8:79:B3:68:73:DD:EB:17
                foreach (RTCDtlsFingerprint finger in dtlsParameters.Fingerprints)
                {
                    sb.Append("a=fingerprint:");
                    sb.Append(finger.Algorithm);
                    sb.Append(' ');
                    sb.Append(finger.Value);
                    sb.Append("\r\n");
                }

                //a = setup:actpass
                sb.Append("a=setup:");
                switch (dtlsParameters.Role)
                {
                    case RTCDtlsRole.Server:
                    case RTCDtlsRole.Client:
                    case RTCDtlsRole.Auto:
                        sb.Append("actpass");
                        break;
                    default:
                        sb.Append("actpass");
                        break;
                }
                sb.Append("\r\n");

                //a=mid:audio
                sb.Append("a=mid:audio");
                sb.Append("\r\n");

                //a = extmap:1 urn:ietf:params:rtp-hdrext:ssrc-audio-level
                //a = extmap:3 http://www.webrtc.org/experiments/rtp-hdrext/abs-send-time

                foreach (RTCRtpHeaderExtension extension in audioCapabilities.HeaderExtensions)
                {
                    sb.Append("a=extmap:");
                    sb.Append(extension.PreferredId);
                    sb.Append(' ');
                    sb.Append(extension.Uri);
                    sb.Append("\r\n");
                }
                //a=sendrecv
                sb.Append("a=sendrecv");    //WARNING - THIS IS HARDCODED, IS SHOULD BE HANDLED PROPERLY
                sb.Append("\r\n");

                //a=rtcp-mux
                sb.Append("a=rtcp-mux");    //WARNING - THIS IS HARDCODED, IS SHOULD BE HANDLED PROPERLY
                sb.Append("\r\n");

                uint maxPTime = 0;
                //a=rtpmap:111 opus/48000/2
                if (audioCapabilities != null)
                {
                    foreach (RTCRtpCodecCapability cap in audioCapabilities.Codecs)
                    {
                        if (maxPTime == 0)
                            maxPTime = cap.Maxptime;
                        else
                            maxPTime = maxPTime > cap.Maxptime ? cap.Maxptime : maxPTime;

                        sb.Append("a=rtpmap:");
                        sb.Append(cap.PreferredPayloadType);
                        sb.Append(' ');
                        sb.Append(cap.Name);
                        sb.Append('/');
                        sb.Append(cap.ClockRate);
                        sb.Append('/');
                        sb.Append(cap.NumChannels);
                        sb.Append("\r\n");
                        //a=fmtp:111 minptime=10; useinbandfec=1
                        //WARNING - THIS SHOULD BE HANDLED BUT MINPTIME AND USEINBANDFEC ARE MISSING 
                    }
                    maxPTime = maxPTime == 0 ? 120 : maxPTime;
                    //a=maxptime:60
                    sb.Append("a=maxptime:");
                    sb.Append(maxPTime);
                    sb.Append("\r\n");
                }

        
                if (ssrcId == 0)
                    ssrcId = (UInt32)Guid.NewGuid().GetHashCode();
                if (cnameSSRC == null)
                    cnameSSRC = Guid.NewGuid().ToString();
                if (audioSSRCLabel == null)
                    audioSSRCLabel = Guid.NewGuid().ToString();

                //a = ssrc:3063731557 cname: 6dj1AlWMKSYOn3hv
                sb.Append("a=ssrc:");
                sb.Append(ssrcId);
                sb.Append(' ');
                sb.Append("cname: ");
                sb.Append(cnameSSRC);
                sb.Append("\r\n");

                //a = ssrc:3063731557 msid: stream_label_1c26693 audio_label_3ea802dc
                sb.Append("a=ssrc:");
                sb.Append(ssrcId);
                sb.Append(' ');
                sb.Append("msid: ");    
                sb.Append(_localStream.Id);
                sb.Append(' ');
                sb.Append(audioSSRCLabel);
                sb.Append("\r\n");
                //a = ssrc:3063731557 mslabel: stream_label_1c26693
                sb.Append("a=ssrc:");
                sb.Append(ssrcId);
                sb.Append(' ');
                sb.Append("mslabel: ");
                sb.Append(_localStream.Id);
                sb.Append("\r\n");
                //a = ssrc:3063731557 label: audio_label_3ea802dc
                sb.Append("a=ssrc:");
                sb.Append(ssrcId);
                sb.Append(' ');
                sb.Append("label: ");
                sb.Append(audioSSRCLabel);
                sb.Append("\r\n");
            }
            string ret = sb.ToString();
            return ret;
            
        }
        public IAsyncOperation<RTCSessionDescription> CreateOffer()
        {
            Task<RTCSessionDescription> ret = Task.Run<RTCSessionDescription>(() =>
            {
                RTCSessionDescription sd = new RTCSessionDescription(RTCSdpType.Offer, this.createSDP());
                return sd;
            });

            return ret.AsAsyncOperation<RTCSessionDescription>();
        }
        public Task SetLocalDescription(RTCSessionDescription description) //async
        {
            Task ret = Task.Run(() =>
            {
                //TODO update modifications
            });
            return ret;
        }

        public Task AddIceCandidate(RTCIceCandidate candidate) //async
        {
            return null;
        }

        private void PrepareGatherer()
        {
            try
            {
                iceGatherer = new ortc_winrt_api.RTCIceGatherer(options);
                iceGatherer.OnICEGathererStateChanged += OnICEGathererStateChanged;
                iceGatherer.OnICEGathererLocalCandidate += this.RTCIceGatherer_onICEGathererLocalCandidate;
                iceGatherer.OnICEGathererCandidateComplete += this.RTCIceGatherer_onICEGathererCandidateComplete;
                iceGatherer.OnICEGathererLocalCandidateGone += this.RTCIceGatherer_onICEGathererLocalCandidateGone;
                iceGatherer.OnICEGathererError += this.RTCIceGatherer_onICEGathererError;

                iceGathererRTCP = iceGatherer.CreateAssociatedGatherer();
                //iceGathererRTCP.OnICEGathererStateChanged += OnICEGathererStateChanged;
                iceGathererRTCP.OnICEGathererLocalCandidate += this.RTCIceGatherer_onRTCPICEGathererLocalCandidate;
                //iceGathererRTCP.OnICEGathererCandidateComplete += this.RTCIceGatherer_onICEGathererCandidateComplete;
                //iceGathererRTCP.OnICEGathererLocalCandidateGone += this.RTCIceGatherer_onICEGathererLocalCandidateGone;
                //iceGathererRTCP.OnICEGathererError += this.RTCIceGatherer_onICEGathererError;
            }
            catch (Exception e)
            {
                return;
            }
            iceTransport = new ortc_winrt_api.RTCIceTransport(iceGatherer);
        }

        private void OnICEGathererStateChanged(RTCIceGathererStateChangeEvent evt)
        {
            if (evt.State == RTCIceGathererState.Complete)
            {
                
            }
        }

        private void RTCIceGatherer_onICEGathererLocalCandidate(RTCIceGathererCandidateEvent evt)
        {
            var iceEvent = new RTCPeerConnectionIceEvent();
            iceEvent.Candidate = Helper.ToWrapperIceCandidate(evt.Candidate, 1); //RTP component
            if (_OnIceCandidate != null)
                _OnIceCandidate(iceEvent);
        }

        private void RTCIceGatherer_onRTCPICEGathererLocalCandidate(RTCIceGathererCandidateEvent evt)
        {
            var iceEvent = new RTCPeerConnectionIceEvent();
            iceEvent.Candidate = Helper.ToWrapperIceCandidate(evt.Candidate, 2); //RTCP component
            if (_OnIceCandidate != null)
                _OnIceCandidate(iceEvent);
        }

        private void RTCIceGatherer_onICEGathererCandidateComplete(RTCIceGathererCandidateCompleteEvent evt)
        {
            int i = 0;
            i++;
        }

        private void RTCIceGatherer_onICEGathererLocalCandidateGone(RTCIceGathererCandidateEvent evt)
        {
            int i = 0;
            i++;
        }

        private void RTCIceGatherer_onICEGathererError(RTCIceGathererErrorEvent evt)
        {
            int i = 0;
            i++;
        }
    }
}
