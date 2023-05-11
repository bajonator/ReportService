using Cipher;
using EmailSender;
using NLog;
using ReportService.Core;
using ReportService.Core.Repositrories;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace ReportService
{
    public partial class ReportService : ServiceBase
    {
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private int _sendHour;
        private int _intervalInMinutes;
        private Timer _timer;
        private ErrorRepository _errorRepository = new ErrorRepository();
        private ReportRepository _reportRepository = new ReportRepository();
        private Email _email;
        private GenerateHtmlEmail _htmlEmail = new GenerateHtmlEmail();
        private string _emailReciver;
        private StringCipher _stringCipher = new StringCipher("228BEF0C-9EAB-43E0-AE03-416CBD13D657");
        private const string NotEncryptedPasswordPrefix = "encrypt:";
        public ReportService()
        {
            InitializeComponent();

            try
            {
                _intervalInMinutes = Convert.ToInt32(ConfigurationManager.AppSettings["UserIntervalInMinute"]);
                _timer = new Timer(_intervalInMinutes * 60000);
                _sendHour = Convert.ToInt32(ConfigurationManager.AppSettings["UserHour"]);
                _emailReciver = ConfigurationManager.AppSettings["ReceiverEmail"];

                _email = new Email(new EmailParams
                {                   
                    HostSmtp = ConfigurationManager.AppSettings["HostSmtp"],
                    Port = Convert.ToInt32(ConfigurationManager.AppSettings["Port"]),
                    EnableSsl = Convert.ToBoolean(ConfigurationManager.AppSettings["EnableSsl"]),
                    SenderName = ConfigurationManager.AppSettings["SenderName"],
                    SenderEmail = ConfigurationManager.AppSettings["SenderEmail"],
                    SenderEmailPassword = DecryptSenderEmailPassword(),
                });
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.Message);
                throw new Exception(ex.Message);
            }
        }

        private string DecryptSenderEmailPassword()
        {
            var encryptedPassword = ConfigurationManager.AppSettings["SenderEmailPassword"];

            if (encryptedPassword.StartsWith(NotEncryptedPasswordPrefix))
            {
                encryptedPassword = _stringCipher.Encrypt(encryptedPassword.Replace(NotEncryptedPasswordPrefix, ""));

                var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                configFile.AppSettings.Settings["SenderEmailPassword"].Value = encryptedPassword;
                configFile.Save();
            }

            return _stringCipher.Decrypt(encryptedPassword);
        }

        protected override void OnStart(string[] args)
        {
            _timer.Elapsed += DoWork;
            _timer.Start();
            logger.Info("Service started...");
        }

        private async void DoWork(object sender, ElapsedEventArgs e)
        {
            try
            {
                await SendError();
                if (Convert.ToBoolean(ConfigurationManager.AppSettings["IfWantSendingReport"]))
                    await SendReport();
            }
            catch (Exception ex)
            {
                logger.Error(ex, ex.Message);
                throw new Exception(ex.Message);
            }
        }

        private async Task SendError()
        {
            var errors = _errorRepository.GetLastErrors(_intervalInMinutes);
            if (errors == null || !errors.Any())
                return;

            await  _email.Send("Błędy w aplikacji", _htmlEmail.GenerateErrors(errors, _intervalInMinutes), _emailReciver);

            logger.Info("Error sent.");

        }

        private async Task SendReport()
        {
            var actualHour = DateTime.Now.Hour;

            if (actualHour < _sendHour)
                return;

            var report = _reportRepository.GetLastNotSentReport();
            if (report == null) return;

            await _email.Send("Raport dobowy", _htmlEmail.GenerateReport(report), _emailReciver);

            _reportRepository.ReportSent(report);

            logger.Info("Report sent.");

        }

        protected override void OnStop()
        {
            logger.Info("Service stopped...");

        }
    }
}
