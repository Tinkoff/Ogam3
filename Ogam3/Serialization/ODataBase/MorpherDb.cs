using System.Collections.Generic;
using System.Linq;
using Ogam3.Lsp;

namespace Ogam3.Serialization.ODataBase {
    public class MorpherDb {
        public static List<ItemDb> Sequence(object node, string objectId) {
            var lst = new List<ItemDb>();
            var stack = new Stack<ValueDb>();
            stack.Push(new ValueDb(node));

            var isRoot = true;

            while (stack.Any()) {
                var itm = stack.Pop();

                if (itm.Value is Cons) {
                    var cons = itm.Value as Cons;
                    var car = cons.Car();
                    var cdr = cons.Cdr();
                    ValueDb carContainer = null;
                    ValueDb cdrContainer = null;

                    if (car != null) {
                        carContainer = new ValueDb(car) { ObjectId = objectId };
                        stack.Push(carContainer);
                    }

                    if (cdr != null) {
                        cdrContainer = new ValueDb(cdr) { ObjectId = objectId };
                        stack.Push(cdrContainer);
                    }

                    if (car != null || cdr != null) {
                        var relation = new ConsDb(carContainer?.Id, cdrContainer?.Id) { ObjectId = objectId };

                        if (isRoot) {
                            relation.Id = objectId;
                            isRoot = false;
                        }

                        var carRef = (lst.OfType<ConsDb>()).FirstOrDefault(i => i.CarId == itm.Id);

                        if (carRef != null) {
                            carRef.CarId = relation.Id;
                        }

                        var cdrRef = (lst.OfType<ConsDb>()).FirstOrDefault(i => i.CdrId == itm.Id);

                        if (cdrRef != null) {
                            cdrRef.CdrId = relation.Id;
                        }

                        lst.Add(relation);
                    }
                } else {
                    lst.Add(itm);
                }
            }

            return lst;
        }

        struct ConsTask {
            public readonly string CarId;
            public readonly string CdrId;
            public readonly Cons Cons;

            public ConsTask(string carId, string cdrId) {
                CarId = carId;
                CdrId = cdrId;
                Cons = new Cons();
            }
        }


        public static object Chain(List<ItemDb> lst) {
            var root = lst.FirstOrDefault(i => i.Id == lst.FirstOrDefault()?.ObjectId);
            var rootRel = root as ConsDb;
            var callStack = new Stack<ConsTask>();

            if (rootRel == null) {
                return (root as ValueDb)?.Value;
            }

            callStack.Push(new ConsTask(rootRel.CarId, rootRel.CdrId));

            var consRoot = callStack.Peek().Cons;

            while (callStack.Any()) {
                var task = callStack.Pop();

                var carObj = lst.FirstOrDefault(a => a.Id == task.CarId);

                if (carObj is ConsDb) {
                    var carCons = carObj as ConsDb;
                    callStack.Push(new ConsTask(carCons.CarId, carCons.CdrId));
                    task.Cons.SetCar(callStack.Peek().Cons);
                } else {
                    task.Cons.SetCar((carObj as ValueDb)?.Value);
                }

                var cdrObj = lst.FirstOrDefault(a => a.Id == task.CdrId);

                if (cdrObj is ConsDb) {
                    var cdrCons = cdrObj as ConsDb;
                    callStack.Push(new ConsTask(cdrCons.CarId, cdrCons.CdrId));
                    task.Cons.SetCdr(callStack.Peek().Cons);
                } else {
                    task.Cons.SetCdr((cdrObj as ValueDb)?.Value);
                }
            }

            return consRoot;
        }
    }
}
