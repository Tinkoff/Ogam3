using Ogam3.Lsp;

namespace CommonInterface {
    [Enviroment ("Examples")]
    public interface IExampleInterface {
        int IntSumm(int a, int b);
        double DoubleSumm(double a, double b);
        void WriteMessage(string text);

        void NotImplemented();
    }
}