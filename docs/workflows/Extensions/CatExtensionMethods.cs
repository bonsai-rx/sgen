
namespace PersonAndDiscriminatedPets
{
    partial class Cat
    {
        public static Cat operator +(Cat c1, Cat c2)
        {
            return new Cat
            {
                CanMeow = c1.CanMeow || c2.CanMeow,
                Age = c1.Age + c2.Age
            };
        }
    }
}
