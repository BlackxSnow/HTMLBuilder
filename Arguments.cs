using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HTMLBuilder
{
    public static class Arguments
    {
        public static Program.Argument Read(in Program.Argument[] args, int index)
        {
            if (index > args.Length - 1)
            {
                Console.WriteLine($"Expected argument {index} but only {args.Length} were provided.");
                throw new ArgumentException();
            }
            return args[index];
        }
    }
}
