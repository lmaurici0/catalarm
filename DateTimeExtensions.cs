using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MauiCatAlarm;

public static class DateTimeExtensions
{
    
    public static DateTime NextMinute(this DateTime dateTime)
    {
        return dateTime.AddMinutes(1).AddSeconds(-dateTime.Second);
    }
}
