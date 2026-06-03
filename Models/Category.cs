using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ARInteriorDesignApp.Models
{
    public class Category
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string ImageUrl { get; set; } = string.Empty;

        public List<Furniture>? Furnitures { get; set; }
    }
}