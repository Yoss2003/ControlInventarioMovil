using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControlInventarioMovilMovil.Modelo.API
{
    public class RespuestaSunat
    {
        public string? numeroDocumento { get; set; } 
        public string? nombre { get; set; }          
        public string? estado { get; set; }          
        public string? condicion { get; set; }       
        public string? direccion { get; set; }       
        public string? distrito { get; set; }
        public string? provincia { get; set; }
        public string? departamento { get; set; }
    }
}
