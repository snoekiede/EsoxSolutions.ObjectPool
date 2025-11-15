namespace EsoxSolutions.ObjectPool.Tests.Models
{
    public class Car(string make, string model)
    {
        public string Make { get; set; } = make;
        public string Model { get; set; } = model;

        public static List<Car> GetInitialCars()
        {
            return
            [
                new Car("Ford", "Focus"),
                new Car("Ford", "Fiesta"),
                new Car("Ford", "Mondeo"),
                new Car("Ford", "Mustang"),
                new Car("Citroen", "DS"),
                new Car("Citroen", "C1"),
                new Car("Citroen", "C2")
            ];
        }
    }
}
