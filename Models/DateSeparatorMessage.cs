using System;

namespace GeminiGUI.Models
{
    public class DateSeparatorMessage
    {
        public DateTime Date { get; set; }
        public string DisplayText { get; set; } = string.Empty;
        
        public DateSeparatorMessage(DateTime date)
        {
            Date = date.Date;
            DisplayText = GetDisplayText(date);
        }
        
        private string GetDisplayText(DateTime date)
        {
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);
            
            if (date.Date == today)
                return "Heute";
            else if (date.Date == yesterday)
                return "Gestern";
            else
                return date.ToString("dddd, dd. MMMM yyyy", new System.Globalization.CultureInfo("de-DE"));
        }
    }
}
