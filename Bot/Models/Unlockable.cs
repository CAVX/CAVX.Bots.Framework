using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Models
{
    public class Unlockable
    {
        public static List<Unlockable> GetAll() => new()
        {
            new Unlockable() { Id = 1, Name = "Jobs", Description = "You can now work jobs for more money!", Cost = 100 },
            new Unlockable() { Id = 2, Name = "Expand Shelter", Description = "Your shelter is bigger! More pets will be available soon!", Cost = 500 }
        };

        public int Id {  get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public decimal Cost { get; set; }
    }
}
