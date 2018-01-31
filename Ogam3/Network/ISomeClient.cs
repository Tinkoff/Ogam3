namespace Ogam3.Network {
    public interface ISomeClient {
        object Call(object seq, bool evalResp);
    }
}