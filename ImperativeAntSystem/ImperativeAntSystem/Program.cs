using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

namespace ImperativeAntSystem
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            AntSystem algorithm = new AntSystem();
            double[][] townCoords = readTextFile();
            //so viele Ameisen wie Städte (gleichverteilt)
            int[] antHomes = createAnts(townCoords.Length);
            double q = 10;
            double qStart = 10;
            double rho = 0.99;
            double alpha = 1;
            double beta = 5;
            int nc = 10;
            byte art = 0; //0 = Ant Density; 1 = Ant Quantity; 2 = Ant Cycle
            algorithm.runAlgorithm(townCoords, antHomes, q, qStart, rho, alpha, beta, nc, art);
        }
        private static int[] createAnts(int size)
        {
            int[] antHomes = new int[size];
            for (int i = 0; i < size; i++)
            {
                antHomes[i] = i + 1;
            }
            return antHomes;
        }
        private static double[][] readTextFile()
        {
            string filePath = string.Empty;
            System.IO.StreamReader file;
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = "c:\\";
                openFileDialog.Filter = "TSP files (*.tsp)|*.tsp";
                openFileDialog.FilterIndex = 2;
                openFileDialog.RestoreDirectory = true;
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    filePath = openFileDialog.FileName;
                    file = new System.IO.StreamReader(filePath);
                }
                else
                {
                    return null;
                }
            }
            string line;
            List<double[]> coords = new List<double[]>();
            bool isCoord = false;
            while ((line = file.ReadLine()) != null)
            {
                if (line == "EOF")
                {
                    //Ende der .tsp-Datei erreicht
                    isCoord = false;
                }
                if (isCoord)
                {
                    //.tsp-Datei-Aufbau:
                    //...
                    //NODE_COORD_SECTION
                    //1 23.0000 64.0000
                    //2 16.0000 40.0000
                    //3 23.5000 24.5000
                    //4 10.8764 81.7899
                    //EOF
                    //
                    string[] words = line.Split(' ');
                    double[] tempCoords = new double[2];
                    string[] numberX = words[1].Split('.');
                    string[] numberY = words[2].Split('.');
                    tempCoords[0] = Convert.ToDouble(numberX[0]);
                    if (numberX.Length > 1)
                    {
                        //Aufaddierung von Nachkommastellen
                        tempCoords[0] += Convert.ToDouble(numberX[1]) / numberX[1].Length;
                    }
                    tempCoords[1] = Convert.ToDouble(numberY[0]);
                    if (numberY.Length > 1)
                    {
                        //Aufaddierung von Nachkommastellen
                        tempCoords[0] += Convert.ToDouble(numberY[1]) / numberY[1].Length;
                    }
                    coords.Add(tempCoords);
                }
                if (line == "NODE_COORD_SECTION")
                {
                    //wird diese Zeile gefunden, können ab der nächsten Zeile die Koordinaten eingelesen werden
                    isCoord = true;
                }
            }
            file.Close();
            //Übertragung der Koordinaten aus der Liste in einen Array
            double[][] coordsOut = new double[coords.Count][];
            for (int i = 0; i < coords.Count; i++)
            {
                coordsOut[i] = new double[2] { coords[i][0], coords[i][1] };
            }
            return coordsOut;
        }
    }
    class AntSystem
    {
        private int n; // Anzahl Städte
        private double[][] townCoords; // Koordinatenliste der Städte
        private double[][] d; // Distanzmatrix
        private double[][] eta; // Sichtbarkeitsmatrix
        private int m; // Anzahl Ameisen
        private int[] antHomes; //Startpositionen der Ameisen
        private int[] antPlaces; // Verteilung der Ameisen auf die Städte
        private int[][] tabus; // Tabulisten der Ameisen
        private bool[][] boolTabus; // bool-TabuMatrix der Ameisen
        private int s; // AKtuelle Iteration des aktuellen Zyklus
        private double q; // Pheromonstärke der Ameisen
        private double qStart; // Startwert aller Strecken
        private double[][] tau; // Pheromonmatrix
        private double rho; // Koeffizient der Evaporation
        private double alpha; // Relative Bedeutung der Pheromone
        private double beta; // Relative Bedeutung der Sichtbarkeiten
        private int nc; // Anzahl an Zyklen 
        private byte art; //0=Ant density, 1=Ant Quantity, 2=Ant Cycle;
        Stopwatch sw;
        public AntSystem()
        {
        }
        // Rückgabewert: double[] { Länge , Berechnungsdauer} 

        public double[] runAlgorithm(double[][] townCoords, int[] antHomes, double q, double qStart, double rho, double alpha, double beta, int nc, byte art)
        {
            this.townCoords = townCoords;
            this.antHomes = antHomes;
            this.q = q;
            this.qStart = qStart;
            this.rho = rho;
            this.alpha = alpha;
            this.beta = beta;
            this.nc = nc;
            this.art = art; //0 = Ant Density; 1 = Ant Quantity; 2 = Ant Cycle
            this.sw = new Stopwatch();
            this.n = this.townCoords.Length;
            // Pheromone initialisieren(in halber Matrix/unteres Dreieck)
            //Form:
            //   1
            //   2 2
            //   3 3 3
            //   .......
            this.tau = new double[this.n - 1][];
            for (int i = 0; i < this.tau.Length; i++)
            {
                this.tau[i] = new double[this.n - 1];
                for (int j = 0; j <= i; j++)
                {
                    this.tau[i][j] = this.qStart;
                }
            }
            // Verteilung der Ameisen auf die Städte vornehmen
            this.m = this.antHomes.Length;
            this.antPlaces = new int[this.m];
            for (int i = 0; i < this.m; i++)
            {
                this.antPlaces[i] = this.antHomes[i];
            }
            // Tabulisten und bool-Tabumatrix initialisieren
            this.tabus = new int[this.m][];
            this.boolTabus = new bool[this.m][];
            for (int i = 0; i < this.m; i++)
            {
                this.tabus[i] = new int[this.n];
                this.tabus[i][0] = this.antHomes[i];
                this.boolTabus[i] = new bool[this.n];
                this.boolTabus[i][this.antHomes[i] - 1] = true;//Startstadt streichen
            }
            // Abstandsmatrix initialisieren
            this.d = new double[this.n][];
            double deltaX, deltaY;
            for (int i = 0; i < this.n; i++)
            {
                this.d[i] = new double[this.n];
                for (int j = 0; j < this.n; j++)
                {
                    deltaX = (townCoords[i][0] - townCoords[j][0]);
                    deltaY = (townCoords[i][1] - townCoords[j][1]);
                    this.d[i][j] = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
                    if (i != j && this.d[i][j] == 0)
                    {
                        this.d[i][j] = 1;
                    }
                }
            }
            // Sichtbarkeit (in halber Matrix/unteres Dreieck)
            this.eta = new double[this.n - 1][];
            for (int i = 0; i < this.eta.Length; i++)
            {
                this.eta[i] = new double[this.n - 1];
                for (int j = 0; j <= i; j++)
                {
                    this.eta[i][j] = 1.0 / this.d[i + 1][j];
                }
            }
            this.s = 1;
            // Hilfsvariablen
            int[] shortestPathTowns = new int[this.n]; // Pfad der kürzesten Tour
            double shortestPathLength = 100; // Länge der kürzesten Tour
            int tempNC = 0; // Aktueller Zyklus
            bool ersteTour = true; ; // Prüfwert der allersten gefundenen Tour
            Random r = new Random(); // Instanz zur Generierung von (Pseudo-)Zufallszahlen
            // Berechnung starten
            double[] output = new double[2];
            bool notTheSameTour = true;
            this.sw.Start();
            do // Schleife bis Abbruchkriterium des Algorithmus erfüllt ist
            {
                tempNC++;
                do // Schleife bis alle Städte tabu sind
                {
                    //neue Iterationsphase
                    this.s++;
                    //Streckenwahrscheinlichkeit, Streckenwahl, Gang und Pheromonupdate
                    double[][] newTau = new double[this.n - 1][];
                    for (int i = 0; i < newTau.Length; i++)
                    {
                        newTau[i] = new double[this.n - 1];
                        for (int j = 0; j <= i; j++)
                        {
                            newTau[i][j] = this.tau[i][j] * this.rho;
                        }
                    }
                    int[] newAntPlaces = new int[this.m];
                    for (int i = 0; i < this.m; i++)
                    {
                        //this.n - this.s + 1 = Anzahl freier Städte in aktueller Iterationsphase
                        int[] freeTowns = new int[this.n - this.s + 1];
                        double[] possibilities = new double[this.n - this.s + 1];
                        double sumOfPossibilities = 0;
                        int freeEntry = 0;
                        //Teil-Wahrscheinlichkeiten und ihre Summe sammeln
                        for (int j = 0; j < this.boolTabus[i].Length; j++)
                        {
                            if (this.boolTabus[i][j] == false)
                            {
                                freeTowns[freeEntry] = j + 1;
                                // if()-Abfrage um immer auf untere Dreiecks-Matrix von tau&eta zuzugreifen, ansonsten outOfRange
                                if ((this.antPlaces[i] - 1) > j)
                                {
                                    possibilities[freeEntry] = ((Math.Pow(this.tau[this.antPlaces[i] - 2][j], this.alpha))
                                                              * (Math.Pow(this.eta[this.antPlaces[i] - 2][j], this.beta)));
                                }
                                else
                                {
                                    possibilities[freeEntry] = ((Math.Pow(this.tau[j - 1][this.antPlaces[i] - 1], this.alpha))
                                                              * (Math.Pow(this.eta[j - 1][this.antPlaces[i] - 1], this.beta)));
                                }
                                if (possibilities[freeEntry] < 0.000_000_001)
                                {
                                    //Mindestwahrscheinlichkeit, falls Wahrscheinlichkeit aus Berechnung zu nah an 0
                                    possibilities[freeEntry] = 0.000_000_001;
                                }
                                sumOfPossibilities += possibilities[freeEntry];
                                freeEntry++;
                            }
                        }
                        //Summe aller Wahrscheinlichkeiten verrechnen
                        for (int u = 0; u < possibilities.Length; u++)
                        {
                            possibilities[u] /= sumOfPossibilities;
                        }
                        //Sortieren der ermittelten Wahrscheinlichkeiten per Insertion Sort
                        for (int u = 0; u < possibilities.Length - 1; u++)
                        {
                            for (int w = (u + 1); w > 0; w--)
                            {
                                if (possibilities[w - 1] > possibilities[w])
                                {
                                    double oldPossibility = possibilities[w - 1];
                                    possibilities[w - 1] = possibilities[w];
                                    possibilities[w] = oldPossibility;
                                    int oldTown = freeTowns[w - 1];
                                    freeTowns[w - 1] = freeTowns[w];
                                    freeTowns[w] = oldTown;
                                }
                            }
                        }
                        //Strecke wählen
                        //Simulation des Werfen eines unfairen Würfels
                        double WuerfelWurfErgebnis = r.NextDouble();
                        double sumUp = 0;
                        int chosenTown = 0;
                        //Sicherstellung, dass Summe aller Wahrscheinlichkeiten >= 1
                        possibilities[possibilities.Length - 1] += 0.03;
                        for (int counter = 0; counter < possibilities.Length; counter++)
                        {
                            sumUp += possibilities[counter];
                            if (WuerfelWurfErgebnis < sumUp)
                            {
                                chosenTown = freeTowns[counter];
                                counter = possibilities.Length;
                            }
                        }
                        //Ameisen setzen
                        newAntPlaces[i] = chosenTown;
                        //Tabulisten ergänzen
                        this.tabus[i][this.s - 1] = chosenTown;
                        this.boolTabus[i][chosenTown - 1] = true;
                        //neue Spuren speichern
                        if (this.art == 0)
                        {
                            // if()-Abfrage um immer auf untere Dreiecks-Matrix von newTau zuzugreifen, ansonsten outOfRange
                            if (antPlaces[i] > chosenTown)
                            {
                                newTau[antPlaces[i] - 2][chosenTown - 1] += this.q;
                            }
                            else
                            {
                                newTau[chosenTown - 2][antPlaces[i] - 1] += this.q;
                            }
                        }
                        else if (this.art == 1)
                        {
                            // if()-Abfrage um immer auf untere Dreiecks-Matrix von newTau zuzugreifen, ansonsten outOfRange
                            if (antPlaces[i] > chosenTown)
                            {
                                newTau[antPlaces[i] - 2][chosenTown - 1] += this.q / this.d[antPlaces[i] - 1][chosenTown - 1];
                            }
                            else
                            {
                                newTau[chosenTown - 2][antPlaces[i] - 1] += this.q / this.d[chosenTown - 1][antPlaces[i] - 1];
                            }
                        }
                    }
                    if (art != 2)
                    {
                        this.tau = newTau;
                    }
                    this.antPlaces = newAntPlaces;
                }
                while (this.s < n);
                //Touren vollenden und kürzeste Tour speichern
                bool[][] chosenRoads = new bool[n][];
                int lastEntry = 1;
                for (int i = 0; i < n; i++)
                {
                    chosenRoads[i] = new bool[lastEntry];
                    lastEntry++;
                }
                double[][] newTau2 = new double[this.n - 1][];
                for (int i = 0; i < newTau2.Length; i++)
                {
                    newTau2[i] = new double[this.n - 1];
                    for (int j = 0; j <= i; j++)
                    {
                        newTau2[i][j] = this.tau[i][j] * this.rho;
                    }
                }
                for (int a = 0; a < tabus.Length; a++)
                {
                    double tempPathLength = 0;
                    int[] tempTabu = tabus[a];
                    for (int i = 0; i < (n - 1); i++)
                    {
                        tempPathLength += d[tempTabu[i] - 1][tempTabu[i + 1] - 1];
                        if (tempTabu[i + 1] < tempTabu[i])
                        {
                            chosenRoads[tempTabu[i] - 1][tempTabu[i + 1] - 1] = true;
                        }
                        else
                        {
                            chosenRoads[tempTabu[i + 1] - 1][tempTabu[i] - 1] = true;
                        }
                    }
                    tempPathLength += d[tempTabu[this.s - 1] - 1][tempTabu[0] - 1];
                    if (tempTabu[this.s - 1] < tempTabu[0])
                    {
                        chosenRoads[tempTabu[0] - 1][tempTabu[this.s - 1] - 1] = true;
                    }
                    else
                    {
                        chosenRoads[tempTabu[this.s - 1] - 1][tempTabu[0] - 1] = true;
                    }
                    //Check ob Tour kürzer ist
                    if (tempPathLength < shortestPathLength || ersteTour)
                    {
                        shortestPathLength = tempPathLength;
                        //Liste kürzester Tour wird erst aktualisiert, wenn Tour tatsächlich kürzer
                        shortestPathTowns = new int[n];
                        for (int i = 0; i < (n - 1); i++)
                        {
                            shortestPathTowns[i] = tempTabu[i];
                        }
                        shortestPathTowns[this.s - 1] = tempTabu[this.s - 1];
                        ersteTour = false;
                    }
                    //neue Spuren zwischenspeichern
                    if (this.art == 0) //Ant Density
                    {
                        // if()-Abfrage um immer auf untere Dreiecks-Matrix von newTau2 zuzugreifen, ansonsten outOfRange
                        if (tempTabu[this.s - 1] - 1 > tempTabu[0] - 1)
                        {
                            newTau2[tempTabu[this.s - 1] - 2][tempTabu[0] - 1] += this.q;
                        }
                        else
                        {
                            newTau2[tempTabu[0] - 2][tempTabu[this.s - 1] - 1] += this.q;
                        }
                    }
                    else if (this.art == 1) //Ant Quantity
                    {
                        // if()-Abfrage um immer auf untere Dreiecks-Matrix von newTau2 zuzugreifen, ansonsten outOfRange
                        if (tempTabu[this.s - 1] - 1 > tempTabu[0] - 1)
                        {
                            newTau2[tempTabu[this.s - 1] - 2][tempTabu[0] - 1] += this.q * 1.0 / d[tempTabu[this.s - 1] - 1][tempTabu[0] - 1];
                        }
                        else
                        {
                            newTau2[tempTabu[0] - 2][tempTabu[this.s - 1] - 1] += this.q * 1.0 / d[tempTabu[0] - 1][tempTabu[this.s - 1] - 1];
                        }
                    }
                    else if (this.art == 2) //Ant Cycle
                    {
                        for (int i = 0; i < (n - 1); i++)
                        {
                            // if()-Abfrage um immer auf untere Dreiecks-Matrix von newTau2 zuzugreifen, ansonsten outOfRange
                            if (tempTabu[i] > tempTabu[i + 1])
                            {
                                newTau2[tempTabu[i] - 2][tempTabu[i + 1] - 1] += this.q * 1.0 / tempPathLength;
                            }
                            else
                            {
                                newTau2[tempTabu[i + 1] - 2][tempTabu[i] - 1] += this.q * 1.0 / tempPathLength;
                            }
                        }
                        // if()-Abfrage um immer auf untere Dreiecks-Matrix von newTau2 zuzugreifen, ansonsten outOfRange
                        if (tempTabu[this.s - 1] - 1 > tempTabu[0] - 1)
                        {
                            newTau2[tempTabu[this.s - 1] - 2][tempTabu[0] - 1] += this.q * 1.0 / tempPathLength;
                        }
                        else
                        {
                            newTau2[tempTabu[0] - 2][tempTabu[this.s - 1] - 1] += this.q * 1.0 / tempPathLength;
                        }
                    }
                }
                this.tau = newTau2;
                //Verteilung der Ameisen auf die Städte
                this.m = this.antHomes.Length;
                this.antPlaces = new int[this.m];
                for (int i = 0; i < this.m; i++)
                {
                    this.antPlaces[i] = this.antHomes[i];
                }
                notTheSameTour = true;
                int count = 0;
                lastEntry = 1;
                for (int i = 0; i < n; i++)
                {
                    for (int u = 0; u < lastEntry; u++)
                    {
                        if (chosenRoads[i][u])
                        {
                            count++;
                        }
                    }
                    lastEntry++;
                }
                if (count == n)
                {
                    notTheSameTour = false;
                }
                if (tempNC < this.nc && notTheSameTour)
                {
                    //Tabulisten und boolTabumatrix reinitialisieren
                    this.tabus = new int[m][];
                    for (int i = 0; i < this.m; i++)
                    {
                        this.tabus[i] = new int[n];
                        this.tabus[i][0] = this.antHomes[i];
                        this.boolTabus[i] = new bool[this.n];
                        this.boolTabus[i][this.antHomes[i] - 1] = true;
                    }
                    this.s = 1;
                }
                //Ausgabe des kürzesten Weges
                else
                {
                    string outputS = "kürzester Pfad: ";
                    for (int i = 0; i < n; i++)
                    {
                        outputS += shortestPathTowns[i].ToString();
                        if (i < n - 1)
                        {
                            outputS += " - ";
                        }
                    }
                    outputS += "\n Länge: " + shortestPathLength.ToString();
                    outputS += "\n Dauer: " + sw.Elapsed;
                    outputS += "\n nc: " + tempNC;
                    Console.WriteLine(outputS);
                    Console.ReadLine();
                }
            }
            while (tempNC < this.nc && notTheSameTour); //Abbruchkriterium beschränkt auf Zyklen
            //Rückgabe der Länge der kürzesten Tour und der benötigten Berechnungszeit
            return output;
        }
    }
}
