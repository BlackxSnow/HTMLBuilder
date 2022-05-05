using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HTMLBuilder
{
    public static class Arguments
    {
        public class Argument
        {
            public string Value;
            public bool IsOption;

            public Argument(string value, bool isOption)
            {
                Value = value;
                IsOption = isOption;
            }
        }
        
        public static Argument Read(in Argument[] args, int index)
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
