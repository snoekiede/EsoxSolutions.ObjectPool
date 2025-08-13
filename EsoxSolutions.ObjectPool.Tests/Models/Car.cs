namespace EsoxSolutions.ObjectPool.Tests.Models
{
    public class Car(string make, string model)
    {
        public string Make { get; set; } = make;
        public string Model { get; set; } = model;

        public static List<Car> GetInitialCars()
        {
            List<Car> result =
            [
                new("Ford", "Focus"),
                new("Ford", "Fiesta"),
                new("Ford", "Mondeo"),
                new("Ford", "Mustang"),
                new("Citroen", "DS"),
                new("Citroen", "C1"),
                new("Citroen", "C2")
            ];

            return result;
        }

        
    }
}
