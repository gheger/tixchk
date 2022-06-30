using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace TixChk {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _paste = false;

        private MediaPlayer _player = new MediaPlayer();
        private string _path = @"resources\tickets.xml";

        private static string _regexToMatchQR = @"http:\/\/lecret2018\.ch\/tix\/\?q=\d{10}$";
        private Regex _pattern = new Regex(_regexToMatchQR);

        private string _currentInput = string.Empty;

        private string _qr_url = "http://lecret2018.ch/tix/?q=";

        public List<string> _validTix;
        public List<Ticket> _scannedTix;

        public MainWindow() {
            InitializeComponent();

            var valid_tix_file = File.ReadAllLines(@"resources\known_tix_numbers.txt");
            _validTix = new List<string>(valid_tix_file);
            this.lb_log.Items.Insert(0, string.Format("Chargement de {0} tickets existants", _validTix.Count));

            _player.Open(new Uri(@"resources\warning.mp3", UriKind.Relative));

            DataContext = this;

            var savedList = Deserialize(_path);
            _scannedTix = (savedList != null) ? savedList : new List<Ticket>();
            //lb_tix_scanned_ok.ItemsSource = (savedList != null) ? savedList : new List<Ticket>();
            //_scannedTix = (List<Ticket>)lb_tix_scanned_ok.ItemsSource;

            DataObject.AddPastingHandler(tx_tix_scan_area, OnPaste);

            //CollectionViewSource.GetDefaultView(lb_tix_scanned_ok.ItemsSource).Refresh();
        }

        //Accept QR by pasting it (as done by testing with paste)
        private void OnPaste(object sender, DataObjectPastingEventArgs e) {
            var isText = e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText, true);
            if (!isText) return;

            var text = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;

            _paste = true;

            if (_pattern.Match(tx_tix_scan_area.Text + text).Success) {
                validateTicket(tx_tix_scan_area.Text + text);
                tx_tix_scan_area.Text = string.Empty;
            }
        }

        //Accept QR by typing it (as done by QR code reader)
        private void tx_tix_scan_area_TextChanged(object sender, TextChangedEventArgs e) {
            if (_paste) {
                _paste = false;
                return;
            }

            _currentInput = tx_tix_scan_area.Text;

            if (_pattern.Match(_currentInput).Success) {
                validateTicket(_currentInput);
                tx_tix_scan_area.Text = string.Empty;
                _currentInput = string.Empty;
            }
        }

        private void validateTicket(string qrCode) {
            if (!qrCode.Contains(_qr_url)) {
                lb_log.Items.Insert(0, "QR code invalide");
            } else {
                string tixNr = qrCode.Substring(qrCode.IndexOf(_qr_url) + _qr_url.Length);

                lb_log.Items.Insert(0, string.Format("{0:dd.MM.yyyy HH:mm:ss} - Billet {1} correctement scanné", DateTime.Now, tixNr));

                if (!_scannedTix.Any(s => tixNr.Equals(s.TicketNr))) {

                    //Check if the ticket is anexisting ticket
                    if (!_validTix.Contains(tixNr)) { //NOK
                        TicketError(string.Format("Ticket inconnu ({0})", tixNr));
                        return;
                    }

                    //OK
                    _scannedTix.Insert(0, new Ticket() { Scanned = DateTime.Now, TicketNr = tixNr });
                    TxtTixOk = string.Format("Billets OK ({0})", _scannedTix.Count);

                    Serialize(_scannedTix, _path);
                } else {
                    TicketError(string.Format("Ticket déjà scanné ({0})", tixNr));
                }
            }

            CollectionViewSource.GetDefaultView(lb_tix_scanned_ok.ItemsSource).Refresh();
        }

        private void TicketError(string error) {
            lb_tix_scanned_nok.Items.Insert(0, string.Format("{0:dd.MM.yyyy HH:mm:ss} - {1}", DateTime.Now, error));

            _player.Position = TimeSpan.Zero;
            _player.Play();
        }

        private void tc_search_tix_TextChanged(object sender, TextChangedEventArgs e) {
            CollectionViewSource.GetDefaultView(lb_tix_scanned_ok.ItemsSource).Filter = CustomFilter;
        }

        private bool CustomFilter(object obj) {
            if (string.IsNullOrEmpty(tc_search_tix.Text)) {
                return true;
            } else {
                return ((Ticket)obj).TicketNr.Contains(tc_search_tix.Text);
            }
        }

        private void Serialize(List<Ticket> list, string path) {
            Tickets t = new Tickets();
            t.TicketList = list;
            XmlSerializer ser = new XmlSerializer(typeof(Tickets));
            using (FileStream fs = new FileStream(path, FileMode.Create)) {
                ser.Serialize(fs, t);
            }
        }

        private List<Ticket> Deserialize(string path) {
            lb_log.Items.Insert(0, "Chargement de la liste des billets déjà scannés");

            List<Ticket> list;

            XmlSerializer ser = new XmlSerializer(typeof(Tickets));
            using (FileStream fs = new FileStream(path, FileMode.Open)) {
                list = ((Tickets)ser.Deserialize(fs)).TicketList;
            }

            TxtTixOk = string.Format("Billets OK ({0})", list.Count);

            return list;
        }


        public IList<Ticket> ScannedTix {
            get {
                return _scannedTix;
            }
        }

        private string _txtTixOk;
        public string TxtTixOk {
            get {
                return _txtTixOk;
            }
            set {
                _txtTixOk = value;
                OnPropertyChanged("TxtTixOk");
            }
        }

        private void OnPropertyChanged(string property) {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(property));
            }
        }
    }

    public class Tickets {
        [XmlArray("TicketList"), XmlArrayItem(typeof(Ticket), ElementName = "Ticket")]
        public List<Ticket> TicketList { get; set; }
    }

    [XmlRoot("Tickets")]
    public class Ticket {
        public DateTime Scanned { get; set; }
        public string TicketNr { get; set; }
    }
}
