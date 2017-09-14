using System.IO;

namespace KungHua.BT.BenCode
{
    public interface IBencodingType
    {
        /// <summary>
        /// Encodes the current object onto the specified binary writer.
        /// 把当前对象编码成规格的二进制流
        /// </summary>
        /// <param name="writer">The writer to write to - must not be null 二进制数据流，不能为空</param>
        void Encode(BinaryWriter writer);
    }
}