using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ECSceneCardTool
{
    class IntRangeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value.GetType() != typeof(int))
            {
                return false;
            }

            Type parameterType = parameter.GetType();
            if (parameterType != typeof(string))
            {
                return false;
            }

            string parameterString = (string)parameter;
            string[] parameters = parameterString.Split('|');
            if (parameters.Length != 2)
            {
                return false;
            }
            
            if (!int.TryParse(parameters[0], out int min) || !int.TryParse(parameters[1], out int max))
            {
                return false;
            }

            return (int)value > min && (int)value < max;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
