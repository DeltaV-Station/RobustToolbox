﻿using System;

namespace SS14.Client
{
    public class Program
    {
        /************************************************************************/
        /* program starts here                                                  */
        /************************************************************************/

        [STAThread]
        private static void Main()
        {
            var args = Environment.GetCommandLineArgs();
            GameController GC = new GameController();
        }
    }
}
