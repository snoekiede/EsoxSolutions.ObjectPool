using EsoxSolutions.ObjectPool.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EsoxSolutions.ObjectPool.Tests.Models
{
    public class Car
    {
        public string Make { get; set; }
        public string Model { get; set; }

        public Car(string Make, string Model)
        {
            this.Make = Make;
            this.Model = Model;
        }

        public static List<Car> GetInitialCars()
        {
            List<Car> result = new();

            result.Add(new Car("Ford", "Focus"));
            result.Add(new Car("Ford", "Fiesta"));
            result.Add(new Car("Ford", "Mondeo"));
            result.Add(new Car("Ford", "Mustang"));
            result.Add(new Car("Citroen", "DS"));
            result.Add(new Car("Citroen", "C1"));
            result.Add(new Car("Citroen", "C2"));

            return result;
        }

        
    }
}
