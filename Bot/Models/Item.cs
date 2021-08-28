using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Models
{
    public class Item
    {
        public static List<Item> GetAll() => new()
        {
            new Item() { Id = 1, Name = "Bone", Cost = 0.99m },
            new Item() { Id = 2, Name = "Milk", Cost = 4.50m },
            new Item() { Id = 3, Name = "Seeds", Cost = 2.00m, Special = true },
            new Item() { Id = 4, Name = "Brush", Cost = 15.99m }
        };

        public int Id {  get; set; }
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public bool Special { get; set; } = false;
    }
}
