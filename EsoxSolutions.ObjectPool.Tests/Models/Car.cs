using System.Collections.Generic;

namespace EsoxSolutions.ObjectPool.Tests.Models
{
    public class Car
    {
        public string Make { get; set; }
        public string Model { get; set; }

        public Car(string make, string model)
        {
            Make = make;
            Model = model;
        }

        public static List<Car> GetInitialCars()
        {
            return new List<Car>
            {
                new Car("Ford", "Focus"),
                new Car("Ford", "Fiesta"),
                new Car("Ford", "Mondeo"),
                new Car("Ford", "Mustang"),
                new Car("Citroen", "DS"),
                new Car("Citroen", "C1"),
                new Car("Citroen", "C2")
            };
        }
    }
}
