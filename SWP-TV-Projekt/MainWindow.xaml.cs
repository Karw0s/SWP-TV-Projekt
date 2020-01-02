using System;
using System.Data.Entity;
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
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            InitTvContent();
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
    }
}