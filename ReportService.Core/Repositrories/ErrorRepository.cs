using ReportService.Core.Domains;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReportService.Core.Repositrories
{
    public class ErrorRepository
    {
        public List<Error> GetLastErrors(int intervalMinutes)
        {
            //pobieranie z bazy danych

            return new List<Error>
            {
                new Error {Message = "Błąd testowy1", Date = DateTime.Now },
                new Error {Message = "Błąd testowy2", Date = DateTime.Now },
            };
        }
    }
}
