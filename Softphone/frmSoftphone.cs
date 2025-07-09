using NAudio.CoreAudioApi;
using Ozeki.VoIP;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Windows.Forms;
namespace Softphone
{
    public partial class frmSoftphone : Form
    {
        #region Properties 

        internal static bool isDisplayNameChange = false;
        private NAudio.CoreAudioApi.MMDevice[] arrayDevices = new NAudio.CoreAudioApi.MMDevice[3];
        NAudio.CoreAudioApi.MMDeviceEnumerator Devices = new NAudio.CoreAudioApi.MMDeviceEnumerator();
        public static string[] infoAcc = new string[6];
        List<TimeCall> HistoryCall = new List<TimeCall>();
        private DateTime Today = new DateTime();
        List<string> InfoHistory = new List<string>();
        private int minuteCall;
        private int secondCall;
        private SoundPlayer player;
        private SoftPhoneManager _softPhoneManager = new SoftPhoneManager();
        #endregion

        #region Check Status Variable 
        public static bool isRegister = false;
        public static bool isHold = false;
        public static bool isCalling = false;
        public static bool isComing = false;
        public static bool isRejecting = false;
        public static bool isMissing = true;
        #endregion

        #region Sound Style
        private const int ringing = 0;
        private const int hangup = 1;
        private const int holding = 2;
        private const int calling = 3;
        private const int buttonpress = 4;

        /// <summary>
        /// Calling
        /// Ringing
        /// Hangup
        /// Holding
        /// </summary>
        /// <param name="namefile"></param>
        private void playSound(int nameSound)
        {
            DirectoryInfo di = new DirectoryInfo(Directory.GetCurrentDirectory());
            player = new SoundPlayer();
            try
            {
                switch (nameSound)
                {
                    case ringing:
                        {
                            player.SoundLocation = di.Parent.Parent.FullName + @"\Resource\sounds\ring.wav";
                            player.Load();
                            player.PlayLooping();
                        }
                        break;

                    case hangup:
                        {
                            player.SoundLocation = di.Parent.Parent.FullName + @"\Resource\sounds\hangup.wav";
                            player.Load();
                            player.PlaySync();
                        }
                        break;

                    case holding:
                        {
                            player.SoundLocation = di.Parent.Parent.FullName + @"\Resource\sounds\hold_music.wav";
                            player.Load();
                            player.PlayLooping();
                        }
                        break;
                    case calling:
                        {
                            player.SoundLocation = di.Parent.Parent.FullName + @"\Resource\sounds\calling.wav";
                            player.Load();
                            player.PlayLooping();
                        }
                        break;
                    case buttonpress:
                        {
                            player.SoundLocation = di.Parent.Parent.FullName + @"\Resource\sounds\cell-phone-1-nr9.wav";
                            player.Load();
                            player.Play();
                        }
                        break;

                }

            }
            catch (Exception ex)
            {
                InvokeGUIThread(() =>
                {
                    lb_Log.Items.Add("Error: " + ex);
                    lb_Log.Items.Add(di.Parent.Parent.FullName);

                });
            }

        }
        #endregion

        #region Setup, Loading information account
        public frmSoftphone()
        {
            InitializeComponent();
            FormClosing += FrmSoftphone_FormClosing;

            _softPhoneManager.OnUnRegistered += OnUnRegistered;
            _softPhoneManager.OnIncomingCall += OnIncomingCall;
            _softPhoneManager.SetupAnswered +=  SetupAnswered;
            _softPhoneManager.SetupInCall +=    SetupInCall;
            _softPhoneManager.EndCalling += EndCalling;
            _softPhoneManager.OnHold += OnHold;
            _softPhoneManager.OnRinging += OnRinging;
        }
        private void FrmSoftphone_FormClosing(object sender, FormClosingEventArgs e)
        {
            string file = CreatFile("cfgacc.ino");
            WriteFile(file, infoAcc, 5);

        }
        private void frmSoftphone_Load(object sender, EventArgs e)
        {
            try
            {
                cbxStatus.SelectedIndexChanged += CbxStatus_SelectedIndexChanged;
                this.Text = "Snow";
                this.Width = 302;
                pcSpeaker.BackColor = Color.DarkSlateGray;
                pcMicro.BackColor = Color.DarkSlateGray;
                AsynThread(() =>
                {
                    string file = CreatFile("cfgacc.ino");
                    lb_Log.Items.Add("Reading file - " + ReadFile(file, infoAcc, 5));
                    lb_Log.Items.Add("Setting information Account... ");
                    var devices = Devices.EnumerateAudioEndPoints(DataFlow.All,
    NAudio.CoreAudioApi.DeviceState.Active);
                    arrayDevices = devices.ToArray();

                    _softPhoneManager.InitializeSoftPhone(true,
                                        infoAcc[0],
                                        infoAcc[2],
                                        infoAcc[2],
                                        infoAcc[3],
                                        infoAcc[1],
                                        (infoAcc[4] != "") ? Convert.ToInt32(infoAcc[4]) : 5060);
                    txtNumber.Enabled = false;

                });
                timeCheckStatus.Start();
                this.Text = "Snow-" + infoAcc[0];
            }
            catch (Exception ex)
            {
                lb_Log.Items.Add("Loading error... " + ex);
            }
        }
        private void AsynThread(Action action)
        {
            Invoke(action);
        }

        #endregion
        #region Handling Threading
        private void InvokeGUIThread(Action action)
        {
            try
            {
                Invoke(action);
            }
            catch (Exception ex)
            {

            }
        }
        #endregion
        #region Menu control
        private void btnMinimize_Click(object sender, EventArgs e)
        {
            this.Width = 302;
        }
        private void menuSubVdailpad_Click(object sender, EventArgs e)
        {
            if (menuSubVdailpad.Checked)
            {
                menuSubVdailpad.Checked = false;
            }
            else
            {
                menuSubVdailpad.Checked = true;
            }
        }
        private void accountToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowAccounts();
        }

        private void ShowAccounts()
        {
            frmAccount sipAccount = new frmAccount(_softPhoneManager);
            sipAccount.Show();
            this.Width = 302;
        }

        private void menuParentabout_Click(object sender, EventArgs e)
        {
            this.Width = 826;

        }
        #endregion

        #region Control Incoming
        private void btnAccpect_Click(object sender, EventArgs e)
        {
            Answering();
        }
        private void btnReject_Click(object sender, EventArgs e)
        {
            Rejecting();
        }
        private void btnIgnore_Click(object sender, EventArgs e)
        {
            player.Stop();
        }
        #endregion        

        #region Control Incall (Calling TimeCall)

        #region Status Incall Boolean Variable
        private bool isActiveMute = false;
        private bool isActiveNosound = false;
        private bool isActiveHold = false;
        private bool isActiveRecord = false;
        #endregion
        private void btnSpeaker_Click(object sender, EventArgs e)
        {
            if (!isActiveNosound)
            {
                isActiveNosound = true;
                btnSpeaker.BackColor = Color.DarkOrange;
                _softPhoneManager.StartSpeaker();
            }
            else
            {
                isActiveNosound = false;
                btnSpeaker.BackColor = SystemColors.InfoText;
                _softPhoneManager.StopSpeaker();
            }
        }
        private void btnHold_Click(object sender, EventArgs e)
        {
            if (!isActiveHold)
            {
                isActiveHold = true;
                btnHold.BackColor = Color.DarkOrange;
            }
            else
            {
                isActiveHold = false;
                btnHold.BackColor = SystemColors.InfoText;
            }
            Holding();
        }
        private void btnRecord_Click(object sender, EventArgs e)
        {
            if (!isActiveRecord)
            {
                isActiveRecord = true;
                btnRecord.BackColor = Color.DarkOrange;

            }
            else
            {
                isActiveRecord = false;
                btnRecord.BackColor = SystemColors.InfoText;
            }
        }
        private void btnMute_Click(object sender, EventArgs e)
        {
            if (!isActiveMute)
            {
                isActiveMute = true;
                btnMute.BackColor = Color.DarkOrange;
                _softPhoneManager.StopMicro();
            }
            else
            {
                isActiveMute = false;
                btnMute.BackColor = SystemColors.InfoText;
                _softPhoneManager.StartMicro();
            }
        }
        private void meterSoundMicro_Tick(object sender, EventArgs e)
        {
            int valueSpeakerMeter = (int)
                (Math.Round(arrayDevices[0].AudioMeterInformation.MasterPeakValue
                * 100 + 0.5));
            int valueMicroMeter = (int)
                (Math.Round(arrayDevices[1].AudioMeterInformation.MasterPeakValue
                * 100 + 0.5));

            int xspeaker = (int)(Math.Round((valueSpeakerMeter / 100.0) * 72 + 0.5));
            int xmicro = (int)(Math.Round((valueMicroMeter / 100.0) * 72 + 0.5));

            lblHideMicro.Location = new System.Drawing.Point(10 + xmicro, 88);
            lblHide.Location = new System.Drawing.Point(158 + xspeaker, 88);
            pcSpeaker.Value = valueSpeakerMeter;
            pcMicro.Value = valueMicroMeter;


        }

        #region Control time call
        private void timerCall_Tick(object sender, EventArgs e)
        {
            secondCall++;
            if (secondCall == 60)
            {
                ++minuteCall;
                secondCall = 0;
            }
            string second = secondCall < 10 ? "0" + Convert.ToString(secondCall)
                                                : Convert.ToString(secondCall);
            string minute = minuteCall < 10 ? "0" + Convert.ToString(minuteCall)
                                                : Convert.ToString(minuteCall);
            lblTimerCall.Text = minute + ":" + second;
        }
        #endregion

        #region Control Calling
        private void btnHangup_Click(object sender, EventArgs e)
        {
            Hanguping();
        }
        #endregion

        #endregion

        #region Status account, Register again, Show error


        #region Status => Fail => Registering run again
        private void CbxStatus_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbxStatus.SelectedIndex == 0)
            {
                AsynThread(() =>
                {
                    _softPhoneManager.RetryRegister();
                });
            }
        }
        #endregion

        #region Register again
        private void btnRetry_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            llblRetry.Visible = false;
            AsynThread(() =>
            {
                _softPhoneManager.RefreshRegister();
            });

        }
        private void llblAccount_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ShowAccounts();
        }
        #endregion

        #region Check Status each 2 seconds
        private void timeCheckStatus_Tick(object sender, EventArgs e)
        {
            if (_softPhoneManager.RegState == RegState.RegistrationSucceeded)
            {
                cbxStatus.SelectedIndex = 1;
                pbxStatus.Image = Properties.Resources.presenceAvailable;
                txtNumber.Enabled = true;
                pnlDialPad.Enabled = true;
                btnDial.Enabled = true;
            }
            else
            {
                cbxStatus.SelectedIndex = 0;
                pbxStatus.Image = Properties.Resources.offline;
                txtNumber.Enabled = false;
                pnlDialPad.Enabled = false;
                btnDial.Enabled = false;
            }
            lb_Log.TopIndex = lb_Log.Items.Count - 1;
            if (isDisplayNameChange)
            {
                this.Text = "Snow-" + infoAcc[0];
                isDisplayNameChange = false;
            }
        }
        #endregion

        #endregion
        #region Call, Calling Answer Reject Hangup Hold Ignore

        public void Calling()
        {
            SetupCalling();
            AsynThread(() => _softPhoneManager.Calling(txtNumber.Text));
            RefeshControlCall();



        }
        private void Answering()
        {

            if (_softPhoneManager.Answer())
            {
                InvokeGUIThread(() => { lb_Log.Items.Add("Call accepted."); });
            }
        }
        private void Hanguping()
        {
            if (_softPhoneManager.Hanguping() == CallState.Rejected)
            {
                InvokeGUIThread(() => { lb_Log.Items.Add("Call rejected."); });
            }
            else
            {
                InvokeGUIThread(() => { lb_Log.Items.Add("Call hanged up."); });
            }
        }
        private void Holding()
        {
            if (!isHold)
            {
                _softPhoneManager.Hold();
                isHold = true;
                InvokeGUIThread(() => { playSound(holding); });

            }
            else
            {
                isHold = false;
                _softPhoneManager.Unhold();
                player.Stop();
            }
            SetupHolding();
        }
        private void Rejecting()
        {
            _softPhoneManager.Reject();
        }
        private void Ignoring()
        {

        }
        #endregion


        #region Checking isCalling
        private void btnDial_Click(object sender, EventArgs e)
        {
            if (txtNumber.Text != string.Empty)
            {
                Calling();
                this.Width = 767;
            }
        }
        private void txtNumber_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                if (txtNumber.Text != "")
                {
                    Calling();
                    this.Width = 767;
                }
            }
            InvokeGUIThread(() =>
            {
                playSound(buttonpress);
            });
        }
        #endregion

        #region Handle show DialPad Contact History LogStatus
        private void btnShowcontact_Click(object sender, EventArgs e)
        {
            tvHistory.Visible = false;
            pnlDialPad.Visible = false;
            lb_Log.Visible = false;
            btnShowcontact.BackColor = Color.DarkOrange;
            btnShowHistory.BackColor = SystemColors.InactiveCaption;
            btnShowdialpad.BackColor = SystemColors.InactiveCaption;
            btnShowLog.BackColor = SystemColors.InactiveCaption;
        }
        private void btnShowlog_Click(object sender, EventArgs e)
        {
            tvHistory.Visible = false;
            lb_Log.Visible = true;
            pnlDialPad.Visible = false;
            btnShowLog.BackColor = Color.DarkOrange;
            btnShowcontact.BackColor = SystemColors.InactiveCaption;
            btnShowHistory.BackColor = SystemColors.InactiveCaption;
            btnShowdialpad.BackColor = SystemColors.InactiveCaption;

        }
        private void btnShowdialpad_Click(object sender, EventArgs e)
        {
            tvHistory.Visible = false;
            pnlDialPad.Visible = true;
            lb_Log.Visible = false;
            btnShowdialpad.BackColor = Color.DarkOrange;
            btnShowcontact.BackColor = SystemColors.InactiveCaption;
            btnShowHistory.BackColor = SystemColors.InactiveCaption;
            btnShowLog.BackColor = SystemColors.InactiveCaption;
        }
        private void btnShowHistory_Click(object sender, EventArgs e)
        {
            tvHistory.Visible = true;
            pnlDialPad.Visible = false;
            lb_Log.Visible = false;
            btnShowHistory.BackColor = Color.DarkOrange;
            btnShowcontact.BackColor = SystemColors.InactiveCaption;
            btnShowdialpad.BackColor = SystemColors.InactiveCaption;
            btnShowLog.BackColor = SystemColors.InactiveCaption;
        }

        #endregion

        #region Input DialPad
        private void btn1_Click(object sender, EventArgs e)
        {
            txtNumber.SelectedText += "1";
            InvokeGUIThread(() =>
            {
                playSound(buttonpress);
            });
        }

        private void btn2_Click(object sender, EventArgs e)
        {
            txtNumber.SelectedText += "2";
            InvokeGUIThread(() =>
            {
                playSound(buttonpress);
            });
        }

        private void btn3_Click(object sender, EventArgs e)
        {
            txtNumber.SelectedText += "3";
            InvokeGUIThread(() =>
            {
                playSound(buttonpress);
            });
        }

        private void btn4_Click(object sender, EventArgs e)
        {
            txtNumber.SelectedText += "4";
            InvokeGUIThread(() =>
            {
                playSound(buttonpress);
            });
        }

        private void btn5_Click(object sender, EventArgs e)
        {
            txtNumber.SelectedText += "5";
            InvokeGUIThread(() =>
            {
                playSound(buttonpress);
            });
        }

        private void btn6_Click(object sender, EventArgs e)
        {
            txtNumber.SelectedText += "6";
            InvokeGUIThread(() =>
            {
                playSound(buttonpress);
            });
        }

        private void btn7_Click(object sender, EventArgs e)
        {
            txtNumber.SelectedText += "7";
            InvokeGUIThread(() =>
            {
                playSound(buttonpress);
            });
        }

        private void btn8_Click(object sender, EventArgs e)
        {
            txtNumber.SelectedText += "8";
            InvokeGUIThread(() =>
            {
                playSound(buttonpress);
            });
        }

        private void btn9_Click(object sender, EventArgs e)
        {
            txtNumber.SelectedText += "9";
            InvokeGUIThread(() =>
            {
                playSound(buttonpress);
            });
        }

        private void btnAsterisk_Click(object sender, EventArgs e)
        {
            txtNumber.SelectedText += "*";
            InvokeGUIThread(() =>
            {
                playSound(buttonpress);
            });
        }

        private void btn0_Click(object sender, EventArgs e)
        {
            txtNumber.SelectedText += "0";
            InvokeGUIThread(() =>
            {
                playSound(buttonpress);
            });
        }

        private void btnHashtag_Click(object sender, EventArgs e)
        {
            txtNumber.SelectedText += "#";
            InvokeGUIThread(() =>
            {
                playSound(buttonpress);
            });
        }

        #endregion

        #region Setup When CallEvent Change
        private void OnIncomingCall(object senderr, EventArgs e)
        {
            InvokeGUIThread(() =>
            {
                this.Width = 767;
                lblInfoCall.Text = "InComing";
                lblName.Text = _softPhoneManager.GetCallerDisplay();
                lblNumberCaller.Text = _softPhoneManager.GetCallerID();
                isComing = true;
                pnlControlIncoming.Visible = true;
                RefeshControlCall();
            });
        }
        private void OnUnRegistered(object senderr, EventArgs e)
        {
            InvokeGUIThread(() =>
            {
                lb_Log.Items.Add("SipAccount.StatusInfor()");
                llblRetry.Visible = true;
                llblAccount.Visible = true;
            });
        }
        private void OnHold(object senderr, EventArgs e)
        {
            playSound(holding);
        }
        private void OnRinging(object senderr, EventArgs e)
        {
            if (isCalling)
                playSound(calling);
            else
                playSound(ringing);
        }
        private void SetupInCall(object senderr, EventArgs e)
        {
            player.Stop();
        }
        private void SetupAnswered(object senderr, EventArgs e)
        {
            player.Stop();
            InvokeGUIThread(() =>
            {
                pnlControlIncoming.Visible = false;
                isMissing = false;
                pnlControl.Enabled = true;
                btnHangup.Visible = true;
                lblInfoCall.Text = "InCall";
                RefeshTimerCall();
                timerCall.Start();
                meterSoundMicro.Start();
            });
        }
        private void SetupHolding()
        {
            if (isActiveHold)
            {
                lblInfoCall.Text = "Holding";
            }
            else
            {
                lblInfoCall.Text = "Incall";
            }

        }
        private void EndCalling(object senderr, EventArgs e)
        {

            InvokeGUIThread(() =>
            {
                pnlControl.Enabled = false;
                pnlControlIncoming.Visible = false;
                btnHangup.Visible = false;
                lblInfoCall.Text = "Call Ended";
                this.Width = 302;
                timerCall.Stop();
                lblTimerCall.Visible = false;
                GetInforIDCall();
                meterSoundMicro.Stop();
                playSound(hangup);
            });
        }
        //working on
        private void GetInforIDCall()
        {
            TimeCall infoCallId = new TimeCall();
            infoCallId.timeCall = lblTimerCall.Text;
            if (isCalling)
            {
                infoCallId.idCall = ListIDCall.calling;
                infoCallId.name = txtNumber.Text;
                infoCallId.number = txtNumber.Text;
            }
            else if (isComing)
            {
                if (isRejecting || isMissing) infoCallId.idCall = ListIDCall.missing;
                else infoCallId.idCall = ListIDCall.inComing;
                infoCallId.name = _softPhoneManager.GetCallerDisplay();
                infoCallId.number = _softPhoneManager.GetCallerID();
            }
            Today = DateTime.Now;
            infoCallId.dateCall = Today.ToShortDateString();
            infoCallId.timeDateCall = Today.ToShortTimeString();
            lb_Log.Items.Add(infoCallId.getData());
            HistoryCall.Add(infoCallId);
            InfoHistory.Add(infoCallId.getData());
            txtNumber.Text = string.Empty;
            txtNumber.Focus();
        }
        private void SetupCalling()
        {
            btnHangup.Visible = true;
            isCalling = true;
            lblInfoCall.Text = "Calling";
            lblName.Text = txtNumber.Text;
            lblNumberCaller.Text = txtNumber.Text;
            //isMissing = false;
        }
        private void RefeshIdCall()
        {
            isCalling = false;
            isComing = false;
            isRejecting = false;
            isMissing = true;
        }
        private void RefeshTimerCall()
        {
            lblTimerCall.Text = "00:00";
            lblTimerCall.Visible = true;
            minuteCall = 0;
            secondCall = 0;

        }
        private void RefeshControlCall()
        {
            isActiveHold = false;
            isActiveMute = false;
            isActiveNosound = false;
            isActiveRecord = false;
            btnHold.BackColor = SystemColors.InfoText;
            btnSpeaker.BackColor = SystemColors.InfoText;
            btnMute.BackColor = SystemColors.InfoText;
            btnRecord.BackColor = SystemColors.InfoText;
        }
        #endregion

        #region Handling File ,Setup information Account

        private string CreatFile(string fileName)
        {
            //create path file
            string file = Environment.CurrentDirectory + @"\" + fileName;

            if (!File.Exists(file))
            {
                File.CreateText(file);
            }
            return file;
        }
        private string WriteFile(string fileName, string[] info, int numberLine)
        {
            //writing information into file
            try
            {
                //if (!File.Exists(fileName)) return "Error";
                using (StreamWriter accountConfig = new StreamWriter(fileName))
                {
                    //writing    
                    for (int i = 0; i < numberLine; ++i)
                    {
                        accountConfig.WriteLine(info[i]);
                    }

                }
                return "Succecced";
            }
            catch (Exception ex)
            {
                return "Error" + ex;
            }
        }
        private string ReadFile(string fileName, string[] info, int numberLine)
        {
            //if (!File.Exists(fileName)) return "Error";
            try
            {
                using (StreamReader accountConfig = new StreamReader(fileName))
                {
                    //reading 
                    for (int i = 0; i < numberLine; ++i)
                    {
                        info[i] = accountConfig.ReadLine();
                    }
                }
                return "Succecced";
            }
            catch (Exception ex)
            {
                return "Error" + ex;
            }
        }
        private void DeleteFile(string fileName)
        {
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }
        }

        #endregion
    }
}
