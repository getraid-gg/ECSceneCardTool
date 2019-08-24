using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ECSceneCardTool
{
    class IntGreaterThanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int threshold;
            if (value.GetType() != typeof(int))
            {
                return false;
            }

            Type parameterType = parameter.GetType();
            if (parameterType == typeof(int))
            {
                threshold = (int)parameter;
            }
            else if (parameterType == typeof(short))
            {
                threshold = (short)parameter;
            }
            else if (parameterType == typeof(long))
            {
                threshold = (int)(long)parameter;
            }
            else if (parameterType == typeof(string))
            {
                int.TryParse((string)parameter, out threshold);
            }
            else
            {
                return false;
            }

            return (int)value > threshold;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
