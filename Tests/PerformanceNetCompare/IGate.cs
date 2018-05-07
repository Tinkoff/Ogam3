using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Text;
using System.Threading.Tasks;
using Ogam3.Lsp.Generators;

namespace PerformanceNetCompare {
    [Enviroment ("srv")]
    [ServiceContract]
    public interface IGate {
        [OperationContract]
        [WebInvoke(Method = "POST", ResponseFormat = WebMessageFormat.Json, BodyStyle = WebMessageBodyStyle.Bare,  UriTemplate = "Echo")]
        Dto Echo(Dto dto);
    }
}
