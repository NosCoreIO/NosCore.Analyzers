using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace NosCore.Analyzers.Analyzers
{
    internal static class TextHelper
    {
        public static IOperation Unwrap(IOperation operation)
        {
            while (operation is IConversionOperation conversion)
            {
                operation = conversion.Operand;
            }

            return operation;
        }

        // Literal text with a letter in it, as opposed to a key, a separator or an empty string.
        public static bool IsWrittenText(IOperation operation)
        {
            operation = Unwrap(operation);
            return operation switch
            {
                ILiteralOperation { ConstantValue: { HasValue: true, Value: string text } } => text.Any(char.IsLetter),
                IInterpolatedStringOperation interpolated => interpolated.Parts
                    .OfType<IInterpolatedStringTextOperation>()
                    .Any(part => part.Text.ConstantValue.Value is string text && text.Any(char.IsLetter)),
                IBinaryOperation { OperatorKind: BinaryOperatorKind.Add } binary when binary.Type?.SpecialType == SpecialType.System_String
                    => IsWrittenText(binary.LeftOperand) || IsWrittenText(binary.RightOperand),
                _ => false,
            };
        }
    }
}
