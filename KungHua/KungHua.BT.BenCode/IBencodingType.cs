using System.IO;

namespace KungHua.BT.BenCode
{
    public interface IBencodingType
    {
        /// <summary>
        /// Encodes the current object onto the specified binary writer.
        /// �ѵ�ǰ�������ɹ��Ķ�������
        /// </summary>
        /// <param name="writer">The writer to write to - must not be null ������������������Ϊ��</param>
        void Encode(BinaryWriter writer);
    }
}