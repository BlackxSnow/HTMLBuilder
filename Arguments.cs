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
                string msg = $"Expected argument {index} but only {args.Length} were provided.";
                Console.WriteLine(msg);
                throw new ArgumentException(msg);
            }
            return args[index];
        }
    }
}
