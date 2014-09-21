using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using Microsoft.Phone.Controls;

using System.IO;
using WordNet;
using System.ComponentModel;

namespace Artha4WP
{
    public partial class MainPage : PhoneApplicationPage
    {
        const string resPrefix = "/Artha4WP;component/dict/";
        string[] dictFileNames = new string[] {
                                                "data.noun",
                                                "data.verb",
                                                "data.adj",
                                                "data.adv",
                                                "index.noun",
                                                "index.verb",
                                                "index.adj",
                                                "index.adv"
                                              };
        WordNetEngine wn = null;
        string lastSearch = "";
        BackgroundWorker bgWorker;

        // Constructor
        public MainPage()
        {
            InitializeComponent();

            Dictionary<string, Stream> dictFiles = new Dictionary<string, Stream>(dictFileNames.Length);
            foreach (string dictFile in dictFileNames)
            {
                Uri resUri = new Uri(string.Concat(resPrefix, dictFile), UriKind.Relative);
                dictFiles.Add(dictFile, Application.GetResourceStream(resUri).Stream);
            }

            wn = new WordNet.WordNetEngine(dictFiles);

            bgWorker = new BackgroundWorker();
            bgWorker.DoWork += new DoWorkEventHandler(QueryWNIAndPopulate);
            bgWorker.WorkerReportsProgress = true;
            bgWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(OnListLoadComplete);
        }

        delegate void PopulateRelativesDelegate(String relativeName, List<String> lstTerms);
        private void PopulateRelatives(String relativeFamilyName, List<String> relativeTerms)
        {
            LongListSelector lstRelatives = new LongListSelector() {
                                                                       IsFlatList = true,
                                                                       IsBouncy = true,
                                                                       Margin = new Thickness(0, 50, 0, 0),
                                                                       ItemTemplate = templateRelativeList
                                                                   };
            lstRelatives.SelectionChanged += new SelectionChangedEventHandler(lstRelatives_SelectionChanged);
            lstRelatives.ItemsSource = relativeTerms;

            PivotItem p = new PivotItem();
            p.Header = relativeFamilyName;
            p.Content = lstRelatives;
            pvtRoot.Items.Add(p);
        }

        void QueryWNIAndPopulate(object sender, DoWorkEventArgs e)
        {
            string strQuery = (string) e.Argument;

            // query WordNet interface
            List<WordNetEngine.RelativeFamily> relatives;
            List<WordNetEngine.POS> poses = wn.Lookup(strQuery, out relatives);

            if (poses.Count == 0)
            {
                WordNetEngine.POS dummyPos = new WordNetEngine.POS()
                { posType = 4, Senses = new List<WordNetEngine.Sense>() };
                dummyPos.Senses.Add(new WordNetEngine.Sense()
                { Definition = "Queried term not found! Please try something different." });
                poses = new List<WordNetEngine.POS>();
                poses.Add(dummyPos);
            }

            // set senses to definitions list
            Dispatcher.BeginInvoke(() => { lstPOSs.ItemsSource = poses; });

            // deal with relatives, if any
            foreach (WordNetEngine.RelativeFamily relFamily in relatives)
            {
                Dispatcher.BeginInvoke(new PopulateRelativesDelegate(PopulateRelatives),
                                       relFamily.RelativeFamilyName,
                                       relFamily.Terms);
            }
        }

        private void OnListLoadComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            progressBar.IsIndeterminate = false;
            progressBar.Visibility = Visibility.Collapsed;

            pvtRoot.SelectedIndex = 0;
            defnsScroller.ScrollToVerticalOffset(0.0);
        }

        private void btnFind_Click(object sender, RoutedEventArgs e)
        {
            string strQuery = txtQuery.Text.Trim().ToLower().Replace(' ', '_').Replace('-', '_');

            if (string.CompareOrdinal(strQuery, lastSearch) != 0)
            {
                if (!bgWorker.IsBusy)
                {
                    // clear any pivot item present in the pivot
                    while (pvtRoot.Items.Count > 1)
                        pvtRoot.Items.RemoveAt(1);

                    // query WNI in a background worker thread and populate the results
                    bgWorker.RunWorkerAsync(strQuery);

                    progressBar.IsIndeterminate = true;
                    progressBar.Visibility = Visibility.Visible;

                    lastSearch = strQuery;
                }
            }
        }

        void lstRelatives_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0)
            {
                txtQuery.Text = (string)e.AddedItems[0];
                btnFind.Focus();
                btnFind_Click(sender, new RoutedEventArgs());
            }
        }

        private void txtQuery_GotFocus(object sender, RoutedEventArgs e)
        {
            txtQuery.SelectAll();
        }

        private void txtQuery_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                btnFind.Focus();
                btnFind_Click(sender, new RoutedEventArgs());
            }
        }

        private void txtPOS_GotFocus(object sender, RoutedEventArgs e)
        {
            TextBox txtDefn = (TextBox)sender;
            string strDefinition = txtDefn.Text;
            char [] strDefnAsChars = strDefinition.ToCharArray();
            int i = 0;
            for (i = txtDefn.SelectionStart - 1; ((i >= 0) &&
                char.IsLetterOrDigit(strDefnAsChars[i])); --i) ;
            int selBegin = i + 1;
            for (i = txtDefn.SelectionStart; ((i < strDefnAsChars.Length) &&
                char.IsLetterOrDigit(strDefnAsChars[i])); ++i) ;
            int selEnd = i;
            txtDefn.Select(selBegin, selEnd - selBegin);
        }

        private void Help_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            NavigationService.Navigate(new Uri("/Help.xaml", UriKind.RelativeOrAbsolute));
        }

        private void AboutText_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            NavigationService.Navigate(new Uri("/AboutPage.xaml", UriKind.RelativeOrAbsolute));
        }
    }
}
