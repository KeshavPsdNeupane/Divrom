using System;
using System.ComponentModel;
using System.Globalization;

public class HashedTagTypeConverter : TypeConverter {
	public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
		=> sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

	public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) {
		if (value is string s)
			return new HashedTag(s);
		return base.ConvertFrom(context, culture, value);
	}

	public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType) {
		if (destinationType == typeof(string) && value is HashedTag tag)
			return tag.ToString();
		return base.ConvertTo(context, culture, value, destinationType);
	}
}