using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Markup;

namespace TimeMonitor.WinApp.Controls
{
    public class CultureAwareBinding : Binding
    {
        public CultureAwareBinding() : base()
        {
            ConverterCulture = System.Threading.Thread.CurrentThread.CurrentCulture ;
        }
        public CultureAwareBinding(string path) : base(path) {
            ConverterCulture = System.Globalization.CultureInfo.CurrentCulture;
        }
        //public override object ProvideValue(IServiceProvider serviceProvider)
        //{
        //    return new Binding() {  };
        //}
    }

    //public class TestBinding : Binding
    //{

    //}
}
