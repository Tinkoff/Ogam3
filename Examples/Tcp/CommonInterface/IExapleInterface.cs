/*
 * Copyright © 2018 Tinkoff Bank
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Ogam3.Lsp.Generators;

namespace CommonInterface {
    [Enviroment ("server-side")]
    public interface IServerSide {
        int IntSumm(int a, int b);
        int IntSummOfPower(int a, int b);
        double DoubleSumm(double a, double b);
        void WriteMessage(string text);
        void NotImplemented();
        ExampleDTO TestSerializer(ExampleDTO dto);
        void Subscribe();
        Roots? QuadraticEquation(double? a, double? b, double? c);
    }

    public struct Roots {
        public double? X1;
        public double? X2;
    }

    [Enviroment ("client-side")]
    public interface IClientSide {
        int Power(int x);
        void Notify(string msg);
    }
}