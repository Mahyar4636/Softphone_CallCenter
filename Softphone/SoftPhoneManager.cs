using Ozeki.Media.MediaHandlers;
using Ozeki.VoIP;
using Ozeki.VoIP.SDK;
using System;
using System.Threading;

namespace Softphone
{
    public class SoftPhoneManager : IDisposable
    {

        #region Softphone Method, Properties

        #region Properties 

        private object lockObj = new object();
        public static SIPAccount accountSipInfo;
        public static ISoftPhone _softPhone;
        public static IPhoneLine _phoneLine;
        //public static RegState _phoneLineInformation;
        private IPhoneCall _call;
        private Microphone _microphone = Microphone.GetDefaultDevice();
        private Speaker _speaker = Speaker.GetDefaultDevice();
        private MediaConnector _connector = new MediaConnector();
        private PhoneCallAudioSender _mediaSender = new PhoneCallAudioSender();
        private PhoneCallAudioReceiver _mediaReceiver = new PhoneCallAudioReceiver();
        public static AccountInfo SipAccount = new AccountInfo();
        private CallerInfo Caller = new CallerInfo();

        private bool _inComingCall;
        private EventHandler<CallStateChangedArgs> CallStateChanged;
        private bool isInComingCompleted = false;

        private System.Timers.Timer TimerRegister = new System.Timers.Timer();

        public RegState RegState => _phoneLine?.RegState ?? RegState.NotRegistered;
        #endregion

        #region Initializes Sip, Register, Subcribe events ,Refesh Register

        public SoftPhoneManager()
        {
            TimerRegister.Interval = 5000;
            TimerRegister.Elapsed += TimerRegister_Tick;
        }
        /// <summary>
        /// Initializes the softphone logic
        /// Subscribes change events to get notifications.
        /// Register info event
        /// Incoming call event
        /// </summary>
        public void InitializeSoftPhone(bool registrationRequired, string displayName, string userName, string registerName, string registerPassword, string domainServerHost, int domainServerPort, string proxy = null)
        {
            try
            {
                accountSipInfo = new SIPAccount(registrationRequired,
                                            displayName,
                                            userName,
                                            registerName,
                                            registerPassword,
                                            domainServerHost,
                                            domainServerPort,
                                            proxy);


                string userAgent = "Snow";
                _softPhone = SoftPhoneFactory.CreateSoftPhone(
                    SoftPhoneFactory.GetLocalIP(),
                    7000, 9000/*, userAgent*/);

                //InvokeGUIThread(() =>
                //{
                //    lb_Log.Items.Add("Softphone created!");
                //});

                _softPhone.IncomingCall += softPhone_inComingCall;


                //InvokeGUIThread(() =>
                //{
                //    lb_Log.Items.Add("SIP account created!");
                //});
                _phoneLine = _softPhone.CreatePhoneLine(accountSipInfo);

                _phoneLine.RegistrationStateChanged += phoneLine_PhoneLineInformation;
                //InvokeGUIThread(() =>
                //{
                //    lb_Log.Items.Add("Phoneline created.");
                //});
                _softPhone.RegisterPhoneLine(_phoneLine);

                _inComingCall = false;

                ConnectMedia();
                TimerRegister.Start();
            }
            catch (Exception ex)
            {
                //InvokeGUIThread(() =>
                //{
                //    lb_Log.Items.Add("Local IP error! " + ex);
                //});
            }
        }
        public void RetryRegister()
        {
            SipAccount.RefeshRegister();
            _softPhone.RegisterPhoneLine(_phoneLine);
            TimerRegister.Start();
        }
        public void RefreshRegister()
        {
            _softPhone.UnregisterPhoneLine(_phoneLine);
            _phoneLine = _softPhone.CreatePhoneLine(accountSipInfo);
            _softPhone.RegisterPhoneLine(_phoneLine);
        }
        #endregion

        #region Sounds, Setup and Conect devices
        private void StartDevices()
        {
            StartSpeaker();
            StartMicro();
        }
        private void StopDevices()
        {
            StopMicro();
            StopSpeaker();
        }
        private void ConnectMedia()
        {
            if (_microphone != null)
            {
                _connector.Connect(_microphone, _mediaSender);
            }

            if (_speaker != null)
            {
                _connector.Connect(_mediaReceiver, _speaker);
            }
        }
        private void DisconnectMedia()
        {
            if (_microphone != null)
            {
                _connector.Disconnect(_microphone, _mediaSender);
            }

            if (_speaker != null)
            {
                _connector.Disconnect(_mediaReceiver, _speaker);
            }
        }
        public void StopMicro()
        {
            if (_microphone != null)
            {
                _microphone.Stop();
                //InvokeGUIThread(() => { lb_Log.Items.Add("Microphone Stopped."); });
            }
        }
        public void StartMicro()
        {
            _microphone = Microphone.GetDefaultDevice();
            if (_microphone != null)
            {
                _microphone.Start();
                //InvokeGUIThread(() => { lb_Log.Items.Add("Microphone Started."); });
            }
        }
        public void StopSpeaker()
        {
            if (_speaker != null)
            {
                _speaker.Stop();
                //InvokeGUIThread(() => { lb_Log.Items.Add("Speaker Stopped."); });
            }
        }
        public void StartSpeaker()
        {
            _speaker = Speaker.GetDefaultDevice();
            if (_speaker != null)
            {
                _speaker.Start();
                //InvokeGUIThread(() => { lb_Log.Items.Add("Speaker Started."); });
            }
        }

        #endregion

        #region Handing Events
        public delegate void UIEventHandler(object source, EventArgs e);

        public event UIEventHandler OnUnRegistered;
        public event UIEventHandler OnIncomingCall;
        public event UIEventHandler SetupAnswered;
        public event UIEventHandler SetupInCall;
        public event UIEventHandler EndCalling;

        public event UIEventHandler OnHold;
        public event UIEventHandler OnRinging;

        private void softPhone_inComingCall(object sender, VoIPEventArgs<IPhoneCall> e)
        {
            //InvokeGUIThread(() =>
            //{
            //    lb_Log.Items.Add("Incoming call from: " + e.Item.DialInfo.ToString());
            //}); //tb_Display.Text = "Ringing (" + e.Item.DialInfo.Dialed + ")";
            Caller.Id = e.Item.DialInfo.CallerID;
            Caller.Name = e.Item.DialInfo.CallerDisplay;
            _call = e.Item;
            SubcribedCallEvents();
            _inComingCall = true;

            isInComingCompleted = true;
            OnIncomingCall(this, new EventArgs());

        }
        private void phoneLine_PhoneLineInformation(object sender, RegistrationStateChangedArgs e)
        {

        }
        private void call_CallStateChanged(object sender, CallStateChangedArgs e)
        {
            try
            {

                if (e.State == CallState.Answered)
                {
                    StartDevices();

                    _mediaReceiver.AttachToCall(_call);
                    _mediaSender.AttachToCall(_call);

                    SetupAnswered(this, new EventArgs());
                }

                if (e.State == CallState.InCall)
                {
                    SetupInCall(this, new EventArgs());
                    StartDevices();
                }

                if (e.State.IsCallEnded() || e.State == CallState.Rejected)
                {
                    StopDevices();

                    _mediaReceiver.Detach();
                    _mediaSender.Detach();

                    UnSubcribedCallEvents();
                    if (isInComingCompleted)
                    {
                        isInComingCompleted = false;
                    }

                    _call = null;
                    EndCalling(this, new EventArgs());

                }

                if (e.State == CallState.LocalHeld)
                {
                    StopDevices();
                }
                if (e.State == CallState.RemoteHeld)
                {
                    OnHold(this, new EventArgs());
                }
                if (e.State == CallState.Ringing)
                {
                    OnRinging(this, new EventArgs());
                }
                DispatchAsync(() =>
                {
                    var handler = CallStateChanged;
                    if (handler != null)
                        handler(this, e);
                });
            }
            catch (Exception ex)
            {

            }
        }
        private void DispatchAsync(Action action)
        {
            var task = new WaitCallback(o => action.Invoke());
            ThreadPool.QueueUserWorkItem(task);
        }
        #endregion

        #region Subcribe, Unsubcribe Calling events
        private void SubcribedCallEvents()
        {
            try
            {
                _call.CallStateChanged += (call_CallStateChanged);
            }
            catch (Exception ex)
            {
                //InvokeGUIThread(() => { lb_Log.Items.Add("Error: " + ex); });
            }
        }
        private void UnSubcribedCallEvents()
        {
            try
            {
                _call.CallStateChanged -= (call_CallStateChanged);
            }
            catch (Exception ex)
            {
                //InvokeGUIThread(() => { lb_Log.Items.Add("Error: " + ex); });
            }
        }

        #endregion


        #region Information CallerId, CallerDisplay
        public string GetCallerID()
        {
            return Caller.Id;
        }
        public string GetCallerDisplay()
        {
            return Caller.Name;
        }
        #endregion

        #region Information Issues registering Sip Account
        public string GetInfoError()
        {
            return SipAccount.StatusInfor();

        }

        public void Dispose()
        {
            _softPhone.IncomingCall -= softPhone_inComingCall;
            _phoneLine.RegistrationStateChanged -= phoneLine_PhoneLineInformation;
            _softPhone.UnregisterPhoneLine(_phoneLine);
            _softPhone.Close();
        }
        #endregion
        #region Registering each 5 seconds (Success => Stop ; Fail more 3 => Stop)
        private void TimerRegister_Tick(object sender, EventArgs e)
        {
            lock (lockObj)
            {
                if (!(_phoneLine.RegState == RegState.RegistrationSucceeded))
                {
                    if (_phoneLine.RegState == RegState.Error)
                    {
                        SipAccount.StatusCode_ = _phoneLine.RegistrationInfo.StatusCode;
                        //lb_Log.Items.Add("Registration error ");
                        bool result = SipAccount.Checking();
                        if (!result)
                        {
                            OnUnRegistered(this, new EventArgs());
                            TimerRegister.Stop();
                        }
                        else if (SipAccount.allowRegister_)
                        {
                            _softPhone.RegisterPhoneLine(_phoneLine);
                        }


                    }
                }
            }
        }
        #endregion
        #region Actions
        public void Calling(string txtNumber)
        {
            if (_call != null || !(_phoneLine.RegState == RegState.RegistrationSucceeded))
            {
                throw new Exception("Call error: " +
                    _phoneLine.RegistrationInfo.StatusCode.ToString());
            }
            _call = _softPhone.CreateCallObject(_phoneLine, txtNumber);
            SubcribedCallEvents();
            _call.Start();



        }
        public bool Answer()
        {
            if (_call != null)
            {
                _inComingCall = false;
                _call.Answer();
                return true;
            }
            return false;
        }
        public CallState Hanguping()
        {
            if (_call != null)
            {
                if (_inComingCall && _call.CallState == CallState.Ringing)
                {
                    _call.Reject();
                    return CallState.Rejected;
                }
                else
                {
                    _call.HangUp();
                    _inComingCall = false;
                    return CallState.RemoteHeld;
                }
                _call = null;
            }
            return CallState.Error;
        }

        public void Hold()
        {
                _call.Hold();
        }

        public void Unhold()
        {
                _call.Unhold();
        }

        public void Reject()
        {
                _call.Reject();
        }

        #endregion

        #endregion

    }
}
