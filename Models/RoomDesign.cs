using System;
using System.ComponentModel.DataAnnotations;

namespace ARInteriorDesignApp.Models
{
    public class RoomDesign
    {
        public int Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public string UserEmail { get; set; } = string.Empty;

        [Required]
        public string RoomImagePath { get; set; } = string.Empty;

        public string SelectedFurnitureName { get; set; } = string.Empty;

        public string SelectedFurnitureImage { get; set; } = string.Empty;

        public string AiSuggestion { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}