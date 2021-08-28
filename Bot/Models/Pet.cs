using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bot.Models
{
    public class Pet
    {
        public static List<Pet> GetAll() => new()
        {
            new Pet() { Id = 1, Name = "Dog", Emoji = "🐶", Sound = "WOOF" },
            new Pet() { Id = 2, Name = "Cat", Emoji = "😺", Sound = "MEOW" },
            new Pet() { Id = 3, Name = "Bird", Emoji = "🐦", Sound = "CHIRP" },
            new Pet() { Id = 4, Name = "Hamster", Emoji = "🐹", Sound = "SQUEAK" }
        };

        public int Id {  get; set; }
        public string Name { get; set; }
        public string Emoji { get; set; }
        public string Sound { get; set; }
    }
}
