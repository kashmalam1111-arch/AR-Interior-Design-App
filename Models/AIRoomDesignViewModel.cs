using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace ARInteriorDesignApp.Models
{
    public class AIRoomDesignViewModel
    {
        [Required]
        public string RoomType { get; set; } = string.Empty;

        [Required]
        public string Style { get; set; } = string.Empty;

        [Required]
        public string Budget { get; set; } = string.Empty;

        [Required]
        public string UserInstruction { get; set; } = string.Empty;

        public IFormFile? RoomImage { get; set; }

        public string GeneratedPrompt { get; set; } = string.Empty;

        public string GeneratedImageUrl { get; set; } = string.Empty;

        public string SuggestionText { get; set; } = string.Empty;

        public string ChangeRequest { get; set; } = string.Empty;

        public bool IsImageGenerated { get; set; } = false;
    }
}