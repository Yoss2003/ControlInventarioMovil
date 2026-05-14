using ControlInventarioMovil.Modelo.Interface;
using DocumentFormat.OpenXml.Office.CustomUI;

namespace ControlInventarioMovil.Modelo.Interface
{
    public interface IMarcasRefrescable
    {
        ComboBox CbMarcasPublic { get; }
    }

    public interface ICargosRefrescable
    {
        ComboBox CbCargoPublic { get; }
    }

    public interface IAreasRefrescable
    {
        ComboBox CbAreaPublic { get; }
    }

    public interface IEstadoEmpleadosRefrescable
    {
        ComboBox CbEstadoEmpleadosPublic { get; }
    }

    public interface IEstadoArticulosRefrescable
    {
        ComboBox CbEstadoArticulosPublic { get; }
    }

    public interface ICategoriasRefrescable
    {
        ComboBox CbCategoriasPublic { get; }
    }
    public interface ICondicionRefrescable
    {
        ComboBox CbCondicionPublic { get; }
    }
    public interface IUbicacionRefrescable
    {
        ComboBox CbUbicacionPublic { get; }
    }    
    public interface IProveedoreRefrescable
    {
        ComboBox CbProveedorPublic { get; }
    }
    public interface ITipoContratoRefrescable
    {
        ComboBox CbTipoContratoPublic { get; }
    }
    public interface IPregunta1Refrescable
    {
        ComboBox CbPregunta1Public { get; }
    }
    public interface IPregunta2Refrescable
    {
        ComboBox CbPregunta2Public { get; }
    }
    public interface IPregunta3Refrescable
    {
        ComboBox CbPregunta3Public { get; }
    }

    public interface IUnidadMedidaRefrescable
    {
        ComboBox CbUnidadMedidaPublic { get; }
    }

    public interface IGruposRegistrosRefrescable
    {
        ComboBox CbGruposRegistrosPublic { get; }
    }
}
