using Microsoft.Speech.Recognition;
using Microsoft.Speech.Recognition.SrgsGrammar;
using Microsoft.Speech.Synthesis;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.Entity;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace SWP_TV_Projekt
{
    /// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        private readonly BackgroundWorker worker;
        static SpeechSynthesizer ss;
        static SpeechRecognitionEngine sre;
        private Grammar mainGrammar;
        private Grammar yesNoGrammar;
        private Grammar volumeGrammar;
        private int programToChange;

        public IDictionary<string, Grammar> Grammars = new Dictionary<string, Grammar>();
        public bool SpeechOn { get; set; } = true;

        public MainWindow()
        {
            InitializeComponent();

            worker = new BackgroundWorker();
            worker.DoWork += DoWork;
            worker.RunWorkerAsync();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitTvContent();
        }

        private void SetUpSpeach()
        {
            CultureInfo ci = new CultureInfo("pl-PL");
            ss = new SpeechSynthesizer();
            sre = new SpeechRecognitionEngine(ci);

            ss.SetOutputToDefaultAudioDevice();
            sre.SetInputToDefaultAudioDevice();

            //Srh = new SpeechRecognitionHelper(ss, this);

            sre.SpeechRecognized += Sre_SpeechRecognized;
            
            mainGrammar = new Grammar(".\\Grammars\\TV-Grammar.xml", "rootRule");
            mainGrammar.Enabled = true;
            Grammars.Add("main", mainGrammar);
            yesNoGrammar = new Grammar(".\\Grammars\\TV-VolumeDecisionGrammar.xml", "rootRule");
            yesNoGrammar.Enabled = false;
            Grammars.Add("volumeDecision", yesNoGrammar);
            volumeGrammar = new Grammar(".\\Grammars\\TV-VolumeLevelGrammar.xml", "rootRule");
            volumeGrammar.Enabled = false;
            Grammars.Add("volumeLevel", volumeGrammar);


            sre.LoadGrammar(mainGrammar);
            sre.LoadGrammar(yesNoGrammar);
            sre.LoadGrammar(volumeGrammar);
            sre.RecognizeAsync(RecognizeMode.Multiple);

        }

        private void Sre_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            string txt = e.Result.Text;
            float confidence = e.Result.Confidence;
            if (confidence >= 0.7)
            {
                
                var containsVolume = e.Result.Semantics.ContainsKey("volume");
                
                int program;
                int volume;

                using (var context = new SwpEntities())
                    program = context.Settings.First().TvChannelId;

                if (e.Result.Grammar.Equals(mainGrammar))
                {
                    if (containsVolume)
                    {
                        program = Convert.ToInt32(e.Result.Semantics["programNumber"].Value);
                        volume = Convert.ToInt32(e.Result.Semantics["volume"].Value);

                        setProgramVolume(program, volume);
                    }
                    else
                    {
                        ss.Speak("Czy chcesz zmienić głośność?");

                        changeActiveGrammar("volumeDecision");

                        programToChange = Convert.ToInt32(e.Result.Semantics["programNumber"].Value);
                    }
                }
                else if (e.Result.Grammar.Equals(yesNoGrammar))
                {
                    if (e.Result.Semantics["decision"].Value.Equals("1"))
                    {
                        if (containsVolume)
                        {
                            volume = Convert.ToInt32(e.Result.Semantics["volume"].Value);
                            program = programToChange;

                            setProgramVolume(program, volume);

                            changeActiveGrammar("main");
                        }
                        else
                        {
                            ss.Speak("Podaj poziom glośności");
                            changeActiveGrammar("volumeLevel");
                        }
                    }
                    else
                    {
                        program = programToChange;
                        using (var context = new SwpEntities())
                            volume = context.Settings.First().Volume;

                        changeActiveGrammar("main");

                        setProgramVolume(program, volume);
                    }
                }
                else if (e.Result.Grammar.Equals(volumeGrammar))
                {
                    volume = Convert.ToInt32(e.Result.Semantics["volume"].Value);
                    program = programToChange;

                    setProgramVolume(program, volume);

                    changeActiveGrammar("main");
                }

            }
            else
            {
                ss.Speak("Proszę powtórzyć");
            }
        }

        private void changeActiveGrammar(string active)
        {
            foreach (var grammar in Grammars.Values)
            {
                grammar.Enabled = false;
            }

            Grammars[active].Enabled = true;
        }

        private void setProgramVolume(int p, int v)
        {
            // TODO remove when more programms added
            if (p > 4)
            {
                p = p % 4;
            }

            SetUI(() => {
                ChangeProgram(p);
                ChangeVolume(v);
            });
        }

        private void DoWork(object sender, DoWorkEventArgs e)
        {
            SetUpSpeach();
            ss.Speak("Włączam Telewizor.");
            while (SpeechOn) {; }
        }

        private async void InitTvContent()
        {
            TvChannel currentChannel;
            Setting currentSettings;
            using (var context = new SwpEntities())
            {
                // currentSettings = await Task.Run(() => context.Settings.First()).ConfigureAwait(false);
                currentSettings = context.Settings.FirstAsync().GetAwaiter().GetResult();
                currentChannel = currentSettings.TvChannel;
            }

            SetChannelDetails(currentChannel);
            SetProgramDetails(currentChannel);
            SetVolumeDetails(currentSettings.Volume);
        }

        private void SetChannelDetails(TvChannel currentChannel)
        {
            ChannelLogo.Source = new BitmapImage(new Uri(currentChannel.LogoUrl));
        }

        private void SetProgramDetails(TvChannel currentChannel)
        {
            var program = HttpTvProgramClient.GetProgramAsync(currentChannel.ApiChannelId, 1).GetAwaiter().GetResult();
            var programEntry = program.Data?.First().Entries?.First();
            if (programEntry == null) return;
            var durationTime = programEntry.End - programEntry.Start;
            var viewedTime = DateTimeOffset.Now - programEntry.Start;
            var viewedPercentage = viewedTime.TotalMilliseconds / durationTime.TotalMilliseconds;

            ProgramTitle.Content = programEntry.Title;
            if (programEntry.Photo != null) TvContentImage.Source = new BitmapImage(programEntry.Photo);

            Duration.Content = durationTime.ToString();
            ProgramProgress.Value = viewedPercentage;
        }

        private void SetVolumeDetails(int volume)
        {
            VolLabel.Content = volume;
        }

        private void ChangeProgram(int channelId)
        {
            using (var context = new SwpEntities())
            {
                var currentChannel = context.TvChannels.Find(channelId);
                context.Settings.First().TvChannel = currentChannel;
                context.SaveChanges();

                SetChannelDetails(currentChannel);
                SetProgramDetails(currentChannel);
            }
        }

        private void ChangeVolume(int volumeValue)
        {
            using (var context = new SwpEntities())
            {
                context.Settings.First().Volume = volumeValue;
                context.SaveChanges();

                SetVolumeDetails(volumeValue);
            }
        }


        //TODO to remove
        private void SetToNextChannel(object sender, MouseEventArgs e)
        {
            using (var context = new SwpEntities())
            {
                var currentChannel = context.Settings.First().TvChannel;
                var nextChannel = 1 + currentChannel.Id % 4;
                ChangeProgram(nextChannel);
            }
        }
        public static void SetUI(Action action)
        {
            Application.Current.Dispatcher.Invoke(action);
        }
    }
}