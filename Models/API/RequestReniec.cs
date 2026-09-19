using System.Text.Json.Serialization;

namespace ControlInventarioMovil.Modelo.API
{
    public class RequestReniec
    {
        [JsonPropertyName("first_name")]
        public string? Nombres { get; set; }

        [JsonPropertyName("first_last_name")]
        public string? ApellidoPaterno { get; set; }

        [JsonPropertyName("second_last_name")]
        public string? ApellidoMaterno { get; set; }

        [JsonPropertyName("document_number")]
        public string? NumeroDocumento { get; set; }

        public string NombreCompleto => $"{Nombres} {ApellidoPaterno} {ApellidoMaterno}".Trim();
    }
}