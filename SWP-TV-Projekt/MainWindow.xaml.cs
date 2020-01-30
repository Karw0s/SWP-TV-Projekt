using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Speech.Recognition;
using Microsoft.Speech.Synthesis;

namespace SWP_TV_Projekt
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private static SpeechSynthesizer speechSynthesizer;
        private static SpeechRecognitionEngine sre;
        private readonly IDictionary<string, Grammar> Grammars = new Dictionary<string, Grammar>();
        private readonly BackgroundWorker worker;
        private Grammar changeVolumeGrammar;
        private Storyboard disappearingStoryboard;
        private Grammar mainGrammar;
        private Grammar simpleChangeGrammar;
        private Grammar volumeGrammar;
        private Grammar yesNoGrammar;

        public MainWindow()
        {
            InitializeComponent();

            worker = new BackgroundWorker();
            worker.DoWork += DoWork;
            worker.RunWorkerAsync();

            InitDisappearingAnimation();
        }

        public bool SpeechOn { get; set; } = true;

        private void InitDisappearingAnimation()
        {
            var disappearingAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = new Duration(TimeSpan.FromSeconds(10)),
                DecelerationRatio = 0.6,
                AccelerationRatio = 0.4
            };

            disappearingStoryboard = new Storyboard();
            disappearingStoryboard.Children.Add(disappearingAnimation);
            Storyboard.SetTargetName(disappearingAnimation, Volume.Name);
            Storyboard.SetTargetProperty(disappearingAnimation, new PropertyPath(OpacityProperty));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitTvContent();
        }

        private void SetUpSpeech()
        {
            var ci = new CultureInfo("pl-PL");
            speechSynthesizer = new SpeechSynthesizer();
            sre = new SpeechRecognitionEngine(ci);

            speechSynthesizer.SetOutputToDefaultAudioDevice();
            sre.SetInputToDefaultAudioDevice();

            sre.SpeechRecognized += Sre_SpeechRecognized;

            mainGrammar = new Grammar(".\\Grammars\\TV-Grammar.xml", "rootRule") {Enabled = true};
            Grammars.Add("main", mainGrammar);

            yesNoGrammar = new Grammar(".\\Grammars\\TV-VolumeDecisionGrammar.xml", "rootRule") {Enabled = false};
            Grammars.Add("volumeDecision", yesNoGrammar);

            changeVolumeGrammar = new Grammar(".\\Grammars\\TV-ChangeVolumeGrammar.xml", "rootRule") {Enabled = true};
            Grammars.Add("changeVolume", changeVolumeGrammar);

            volumeGrammar = new Grammar(".\\Grammars\\TV-VolumeLevelGrammar.xml", "rootRule") {Enabled = false};
            Grammars.Add("volumeLevel", volumeGrammar);

            simpleChangeGrammar = new Grammar(".\\Grammars\\SimpleProgVolChangeGrammar.xml", "rootRule")
                {Enabled = true};
            Grammars.Add("simpleChange", simpleChangeGrammar);

            sre.LoadGrammar(mainGrammar);
            sre.LoadGrammar(yesNoGrammar);
            sre.LoadGrammar(changeVolumeGrammar);
            sre.LoadGrammar(volumeGrammar);
            sre.LoadGrammar(simpleChangeGrammar);
            sre.RecognizeAsync(RecognizeMode.Multiple);
        }

        private void Sre_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            var txt = e.Result.Text;
            var confidence = e.Result.Confidence;
            if (confidence >= 0.5)
            {
                if (e.Result.Grammar.Equals(mainGrammar))
                    HandleMainGrammar(e.Result);
                else if (e.Result.Grammar.Equals(changeVolumeGrammar))
                    HandleChangeVolumeGrammar(e.Result);
                else if (e.Result.Grammar.Equals(yesNoGrammar))
                    HandleYesNoGrammar(e.Result);
                else if (e.Result.Grammar.Equals(volumeGrammar))
                    HandleVolumeGrammar(e.Result);
                else if (e.Result.Grammar.Equals(simpleChangeGrammar))
                    HandleSimpleChangeGrammar(e.Result);
            }
            else
            {
                speechSynthesizer.Speak(VoiceCommand.TryAgain);
            }
        }

        private void HandleSimpleChangeGrammar(RecognitionResult eResult)
        {
            int program;
            int volume;

            using (var context = new SwpEntities())
            {
                volume = context.Settings.First().Volume;
            }

            using (var context = new SwpEntities())
            {
                program = context.Settings.First().TvChannelId;
            }

            if (eResult.Semantics.ContainsKey(SemanticKey.Volume))
            {
                var v = Convert.ToInt32(eResult.Semantics["volume"].Value);

                if (v == 0)
                    volume = 0;
                else
                    volume += v;

                ChangeVolume(volume);
            }

            if (eResult.Semantics.ContainsKey("program"))
            {
                var p = Convert.ToInt32(eResult.Semantics["program"].Value);
                program = program + p;

                ChangeProgram(program);
            }
        }

        private void HandleVolumeGrammar(RecognitionResult eResult)
        {
            var volume = Convert.ToInt32(eResult.Semantics[SemanticKey.Volume].Value);
            ChangeVolume(volume);
            ResetActiveGrammars();
        }

        private void HandleYesNoGrammar(RecognitionResult eResult)
        {
            if (eResult.Semantics[SemanticKey.Decision].Value.Equals("1"))
            {
                var containsVolume = eResult.Semantics.ContainsKey(SemanticKey.Volume);
                if (containsVolume)
                {
                    var volume = Convert.ToInt32(eResult.Semantics[SemanticKey.Volume].Value);
                    ChangeVolume(volume);
                    ResetActiveGrammars();
                }
                else
                {
                    ChangeActiveGrammar("volumeLevel");
                    speechSynthesizer.Speak(VoiceCommand.EnterVolumeLevel);
                }
            }
            else
            {
                ResetActiveGrammars();
            }
        }

        private void HandleChangeVolumeGrammar(RecognitionResult eResult)
        {
            var volume = Convert.ToInt32(eResult.Semantics[SemanticKey.Volume].Value);
            ChangeVolume(volume);
        }

        private void HandleMainGrammar(RecognitionResult eResult)
        {
            var containsVolume = eResult.Semantics.ContainsKey(SemanticKey.Volume);
            var containsProgram = eResult.Semantics.ContainsKey(SemanticKey.Channel);
            var containsDetails = eResult.Semantics.ContainsKey(SemanticKey.Details);
            var containsClose = eResult.Semantics.ContainsKey(SemanticKey.Close);
            if (containsClose) CloseProgramDescriptionPanel();
            if (containsDetails && !(containsProgram || containsVolume)) OpenProgramDescriptionPanel();
            if (containsProgram && containsVolume)
            {
                var program = Convert.ToInt32(eResult.Semantics[SemanticKey.Channel].Value);
                var volume = Convert.ToInt32(eResult.Semantics[SemanticKey.Volume].Value);
                SetProgramVolume(program, volume);
            }
            else if (containsProgram)
            {
                var program = Convert.ToInt32(eResult.Semantics[SemanticKey.Channel].Value);
                ChangeProgram(program);
                ChangeActiveGrammar("volumeDecision");
                speechSynthesizer.Speak(VoiceCommand.QuestionToChangingVolume);
            }

            if (containsDetails) OpenProgramDescriptionPanel();
        }

        private void ChangeActiveGrammar(string active)
        {
            foreach (var grammar in Grammars.Values) grammar.Enabled = false;

            Grammars[active].Enabled = true;
        }

        private void ResetActiveGrammars()
        {
            foreach (var grammar in Grammars.Values) grammar.Enabled = false;

            Grammars["main"].Enabled = true;
            Grammars["changeVolume"].Enabled = true;
            Grammars["simpleChange"].Enabled = true;
        }

        private void SetProgramVolume(int channelId, int volumeValue)
        {
            SetUI(() =>
            {
                ChangeProgram(channelId);
                ChangeVolume(volumeValue);
            });
        }

        private void DoWork(object sender, DoWorkEventArgs e)
        {
            SetUpSpeech();
            speechSynthesizer.Speak(VoiceCommand.TvTurnsOn);
            while (SpeechOn) ;
        }

        private void InitTvContent()
        {
            TvChannel currentChannel;
            Setting currentSettings;
            using (var context = new SwpEntities())
            {
                currentSettings = context.Settings.FirstAsync().GetAwaiter().GetResult();
                currentChannel = currentSettings.TvChannel;
            }

            SetChannelDetails(currentChannel);
            SetProgramDetails(currentChannel);
            SetVolumeDetails(currentSettings.Volume);
        }

        private void SetChannelDetails(TvChannel currentChannel)
        {
            SetUI(() => { ChannelLogo.Source = new BitmapImage(new Uri(currentChannel.LogoUrl)); });
        }

        private void SetProgramDetails(TvChannel currentChannel)
        {
            var program = HttpTvProgramClient.GetProgramAsync(currentChannel.ApiChannelId, 1).GetAwaiter().GetResult();
            var programEntry = program.Data?.First().Entries?.First();
            if (programEntry == null) return;
            var durationTime = programEntry.End - programEntry.Start;
            var viewedTime = DateTimeOffset.Now - programEntry.Start;
            var viewedPercentage = viewedTime.TotalMilliseconds / durationTime.TotalMilliseconds;

            SetUI(() =>
            {
                ProgramTitle.Content = programEntry.Title;
                if (programEntry.Photo != null) TvContentImage.Source = new BitmapImage(programEntry.Photo);

                Duration.Content = durationTime.ToString();
                ProgramProgress.Value = viewedPercentage;
                WindowContent.Title = currentChannel.Name;
                ProgramDescription.Text = programEntry.Description;
            });
        }

        private void SetVolumeDetails(int volume)
        {
            SetUI(() =>
            {
                VolLabel.Content = volume;
                disappearingStoryboard.Begin(this);
            });
        }

        private void ChangeProgram(int channelId)
        {
            switch (channelId)
            {
                case -1:
                    SetToPrevChannel();
                    break;
                case 0:
                    SetToNextChannel();
                    break;
                default:
                    SetProgram(channelId);
                    break;
            }
        }

        private void SetProgram(int channelId)
        {
            using (var context = new SwpEntities())
            {
                var currentChannel = context.TvChannels.Find(channelId);
                if (currentChannel == null)
                {
                    speechSynthesizer.Speak(string.Format(VoiceCommand.ChannelNotFound, channelId));
                    return;
                }

                context.Settings.First().TvChannel = currentChannel;
                context.SaveChanges();

                CloseProgramDescriptionPanel();
                SetChannelDetails(currentChannel);
                SetProgramDetails(currentChannel);
            }
        }

        private void ChangeVolume(int volumeValue)
        {
            if (volumeValue > 10)
                volumeValue = 10;
            else if (volumeValue < 0)
                volumeValue = 0;

            using (var context = new SwpEntities())
            {
                context.Settings.First().Volume = volumeValue;
                context.SaveChanges();

                SetVolumeDetails(volumeValue);
            }
        }


        private void SetToNextChannel(object sender, MouseEventArgs e)
        {
            SetToNextChannel();
        }

        private void SetToNextChannel()
        {
            ChangeProgramByPosition(1);
        }

        private void SetToPrevChannel()
        {
            ChangeProgramByPosition(-1);
        }

        private void ChangeProgramByPosition(int position)
        {
            using (var context = new SwpEntities())
            {
                var channelCount = context.TvChannels.Count();
                var currentChannel = context.Settings.First().TvChannel;
                var nextChannel = position + currentChannel.Id % channelCount;
                ChangeProgram(nextChannel);
            }
        }

        private static void SetUI(Action action)
        {
            Application.Current.Dispatcher?.Invoke(action);
        }

        private void OpenProgramDescriptionPanel(object sender, MouseButtonEventArgs e)
        {
            OpenProgramDescriptionPanel();
        }

        private void OpenProgramDescriptionPanel()
        {
            SetUI(() => { ProgramDescriptionPanel.Visibility = Visibility.Visible; });
        }

        private void CloseProgramDescriptionPanel(object sender, MouseButtonEventArgs e)
        {
            CloseProgramDescriptionPanel();
        }

        private void CloseProgramDescriptionPanel()
        {
            SetUI(() => { ProgramDescriptionPanel.Visibility = Visibility.Collapsed; });
        }
    }
}