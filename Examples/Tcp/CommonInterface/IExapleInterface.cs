using Ogam3.Lsp;

namespace CommonInterface {
    [Enviroment ("server-side")]
    public interface IServerSide {
        int IntSumm(int a, int b);
        int IntSummOfPower(int a, int b);
        double DoubleSumm(double a, double b);
        void WriteMessage(string text);

        void NotImplemented();
    }

    [Enviroment ("client-side")]
    public interface IClientSide {
        int Power(int x);
    }
}