namespace Acme.Mapper.CoreTests
{
    public class POCO
    {
        public string sourceproperty;
        public string destinationproperty;

        public POCOComposition source = new POCOComposition();
        public POCOComposition destination = new POCOComposition();
    }

    public class POCOComposition
    {
        public string sourcesubproperty;
        public string destinationsubproperty;
    }
}
