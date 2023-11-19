namespace EsoxSolutions.ObjectPool.Tests.Models
{
    public class Car(string Make, string Model)
    {
        public string Make { get; set; } = Make;
        public string Model { get; set; } = Model;

        public static List<Car> GetInitialCars()
        {
            List<Car> result = new()
            {
                new Car("Ford", "Focus"),
                new Car("Ford", "Fiesta"),
                new Car("Ford", "Mondeo"),
                new Car("Ford", "Mustang"),
                new Car("Citroen", "DS"),
                new Car("Citroen", "C1"),
                new Car("Citroen", "C2")
            };

            return result;
        }

        
    }
}
